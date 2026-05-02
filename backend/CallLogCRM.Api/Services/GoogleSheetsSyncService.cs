using CallLogCRM.Api.Data;
using CallLogCRM.Api.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.EntityFrameworkCore;

namespace CallLogCRM.Api.Services;

/// <summary>
/// Background service that periodically fetches daily call assignments from a
/// Google Sheet and upserts them into the <see cref="CallReservation"/> table.
///
/// Actual spreadsheet columns (A–H, starting at row 2):
///   [0] Source   [1] Nom   [2] Email   [3] Telephone
///   [4] (unused) [5] Date Rendez-Vous  [6] Closeuse   [7] Statut Call
///
/// Separator rows ("JOUR X", empty lines, etc.) are skipped automatically:
/// any row missing a phone number in [3] or a name in [1] is ignored.
/// </summary>
public sealed class GoogleSheetsSyncService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<GoogleSheetsSyncService> _logger;
    private readonly string _spreadsheetId;
    private readonly string _range;
    private readonly string _credentialsPath;
    private readonly int _syncIntervalMinutes;

    public GoogleSheetsSyncService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<GoogleSheetsSyncService> logger)
    {
        _services            = services;
        _logger              = logger;
        _spreadsheetId       = config["GoogleSheets:SpreadsheetId"] ?? string.Empty;
        _range               = config["GoogleSheets:Range"]         ?? "A2:I";
        _credentialsPath     = config["GoogleSheets:CredentialsPath"] ?? "google-credentials.json";
        _syncIntervalMinutes = int.TryParse(config["GoogleSheets:SyncIntervalMinutes"], out var m) ? m : 720;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "GoogleSheetsSyncService started — syncing every {Interval} min from sheet {Id}",
            _syncIntervalMinutes, _spreadsheetId);

        // Run an initial sync immediately on startup, then enter the periodic loop.
        await SyncAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_syncIntervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncAsync(stoppingToken);
        }
    }

    // ── Core sync logic ─────────────────────────────────────────

    private async Task SyncAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Google Sheets sync cycle starting…");

            var rows = await FetchSheetRowsAsync(ct);
            if (rows is null || rows.Count == 0)
            {
                _logger.LogWarning("No rows returned from Google Sheets.");
                return;
            }

            await UpsertReservationsAsync(rows, ct);

            _logger.LogInformation("Google Sheets sync cycle complete — processed {Count} rows.", rows.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Application is shutting down — exit gracefully.
            _logger.LogInformation("Sync cancelled due to application shutdown.");
        }
        catch (Exception ex)
        {
            // Catch-all so the hosted service doesn't crash the process.
            _logger.LogError(ex, "Google Sheets sync failed. Will retry on the next tick.");
        }
    }

    // ── Google Sheets API ───────────────────────────────────────

    private async Task<IList<IList<object>>?> FetchSheetRowsAsync(CancellationToken ct)
    {
        GoogleCredential credential;
        await using (var stream = File.OpenRead(_credentialsPath))
        {
#pragma warning disable CS0618 // GoogleCredential.FromStreamAsync is obsolete but the replacement (CredentialFactory) is internal in this SDK version.
            credential = (await GoogleCredential.FromStreamAsync(stream, ct))
                .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);
#pragma warning restore CS0618
        }

        using var sheetsService = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName       = "CallLogCRM-Sync"
        });

        var request  = sheetsService.Spreadsheets.Values.Get(_spreadsheetId, _range);
        var response = await request.ExecuteAsync(ct);

        return response.Values;
    }

    // ── Database upsert ─────────────────────────────────────────

    private async Task UpsertReservationsAsync(IList<IList<object>> rows, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── Purge-on-sync ────────────────────────────────────────────────────
        // Delete ALL existing reservations before re-importing.
        // This guarantees the DB always reflects the current sheet state and
        // eliminates any previously mis-mapped rows.
        var purged = await db.CallReservations.ExecuteDeleteAsync(ct);
        _logger.LogInformation("Purged {Count} stale reservations before re-sync.", purged);

        // ── Load users once for closer-name → ID lookup ──────────────────────
        var users = await db.Users.AsNoTracking().ToListAsync(ct);
        var userMap = users.ToDictionary(u => u.CloserName, u => u.Id,
            StringComparer.OrdinalIgnoreCase);

        var pending = new List<CallReservation>();
        var skipped = 0;

        foreach (var row in rows)
        {
            // ── STEP 1: skip separator / garbage rows ────────────────────────
            // Minimum meaningful row = 6 cells: col-A absent AND Status stripped,
            // but the closer name still present at index 5.  Anything shorter is
            // a "JOUR X" header, blank line, or otherwise unparseable noise.
            if (row.Count < 6)
            {
                skipped++;
                continue;
            }

            // Reject rows whose first non-empty cell contains "JOUR".
            var firstNonEmpty = row.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c?.ToString()))?.ToString() ?? string.Empty;
            if (firstNonEmpty.Contains("JOUR", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            // ── STEP 2: shift-proof offset detection ─────────────────────────
            // The Sheets API strips TRAILING empty cells, so a row's column count
            // depends on which cells are populated, not on the sheet's column count.
            // This creates four possible layouts:
            //
            //  count ≥ 8 → col-A present, Status present
            //               [0]=A [1]=Source [2]=Nom [3]=Email [4]=Phone [5]=Date [6]=Closer [7]=Status
            //               o = 1
            //
            //  count = 7 → AMBIGUOUS — two cases share the same count:
            //    a) col-A absent,  Status present  → [0]=Source … [5]=Closer [6]=Status  → o = 0
            //    b) col-A present, Status stripped  → [0]=A … [5]=Date [6]=Closer         → o = 1
            //    Resolution: probe row[5] and row[6] as closer candidates; whichever
            //    matches a known user determines the offset.  If neither matches, we
            //    default to o=0 and let STEP 3 reject the row cleanly.
            //
            //  count = 6 → col-A absent, Status stripped
            //               [0]=Source [1]=Nom [2]=Email [3]=Phone [4]=Date [5]=Closer
            //               o = 0
            int o;
            if (row.Count >= 8)
            {
                o = 1;
            }
            else if (row.Count == 7)
            {
                var probe0 = row[5]?.ToString()?.Trim() ?? string.Empty; // closer if o=0
                var probe1 = row[6]?.ToString()?.Trim() ?? string.Empty; // closer if o=1
                o = (!userMap.ContainsKey(probe0) && userMap.ContainsKey(probe1)) ? 1 : 0;
            }
            else // count == 6
            {
                o = 0;
            }

            string Cell(int idx) =>
                idx < row.Count ? row[idx]?.ToString()?.Trim() ?? string.Empty
                                : string.Empty;

            var source       = Cell(o);         // Col B: Source / campaign
            var customerName = Cell(o + 1);     // Col C: Client name
            var email        = Cell(o + 2);     // Col D: Email
            var phone        = Cell(o + 3);     // Col E: Phone
            var dateRaw      = Cell(o + 4);     // Col F: Appointment date
            var closerName   = Cell(o + 5);     // Col G: Closer name
            var rawStatus    = Cell(o + 6);     // Col H: Statut Call
            var currentStatus = string.IsNullOrWhiteSpace(rawStatus) ? null : rawStatus;
            var rawNotes     = Cell(o + 7);     // Col I: Call notes written by closer
            var sheetNotes   = string.IsNullOrWhiteSpace(rawNotes)   ? null : rawNotes;

            _logger.LogDebug(
                "Row [cols={Count} offset={O}] name='{Name}' phone='{Phone}' closer='{Closer}' status='{Status}'",
                row.Count, o, customerName, phone, closerName, currentStatus ?? "(null)");

            // ── STEP 3: "Closer First" — a valid closer is the only hard requirement ──
            // Rows without a recognisable closer name are structural noise (headers,
            // separators, blank lines) and are always skipped.
            // Missing phone, client name, or date get safe defaults instead of
            // discarding the row — this is what lets incomplete rows reach Hayat's queue.
            if (!userMap.TryGetValue(closerName, out var userId))
            {
                _logger.LogDebug(
                    "Closer '{Closer}' not in Users table — skipping row (name='{Name}').",
                    closerName, customerName);
                skipped++;
                continue;
            }

            // ── STEP 4: fill defaults for incomplete data ──────────────────────
            if (string.IsNullOrWhiteSpace(customerName))
                customerName = "Client Inconnu";

            if (string.IsNullOrWhiteSpace(phone))
                phone = "Inconnu";

            // Rows with missing or unparseable dates are stored with DateTime.MinValue.
            // The frontend renders these as "À définir" so the row remains clickable.
            var appointmentDate = TryParseDate(dateRaw, out var parsedDate)
                ? parsedDate
                : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

            pending.Add(new CallReservation
            {
                AssignedUserId  = userId,
                CustomerName    = customerName,
                PhoneNumber     = phone,
                Email           = email,
                AppointmentDate = appointmentDate,
                Source          = string.IsNullOrEmpty(source) ? "N/A" : source,
                CurrentStatus   = currentStatus,
                Notes           = sheetNotes
            });
        }

        // ── STEP 5: deduplicate by name ──────────────────────────────────────
        // When a prospect submits the form twice, one row often has a valid phone
        // and the other has "Inconnu".  Group by normalised name and keep the row
        // with the best phone number; fall back to the first row if both are equal.
        var toInsert = pending
            .GroupBy(r => r.CustomerName.Trim().ToLowerInvariant())
            .Select(g => g
                .OrderByDescending(r =>
                    !string.IsNullOrWhiteSpace(r.PhoneNumber) && r.PhoneNumber != "Inconnu" ? 1 : 0)
                .First())
            .ToList();

        var duplicatesRemoved = pending.Count - toInsert.Count;
        if (duplicatesRemoved > 0)
            _logger.LogInformation("Deduplication removed {Count} duplicate row(s).", duplicatesRemoved);

        // ── STEP 6: insert ───────────────────────────────────────────────────
        foreach (var reservation in toInsert)
            db.CallReservations.Add(reservation);

        if (toInsert.Count > 0)
            await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Sync complete: {Inserted} inserted, {Skipped} skipped, {Dupes} deduplicated.",
            toInsert.Count, skipped, duplicatesRemoved);
    }

    // ── Date parsing helper ─────────────────────────────────────

    /// <summary>
    /// Attempts to parse the date string from the sheet.  Handles common French
    /// formats (dd/MM/yyyy, dd-MM-yyyy) and ISO (yyyy-MM-dd), plus datetime variants.
    /// ISO 8601 UTC strings from Google Sheets (e.g. "2026-04-14T09:30:00.000Z") are
    /// handled by the RoundtripKind fallback which preserves both the time and UTC kind.
    /// </summary>
    private static bool TryParseDate(string raw, out DateTime result)
    {
        // Try explicit French/date-only formats first.
        string[] formats =
        [
            "dd/MM/yyyy",
            "dd-MM-yyyy",
            "dd/MM/yyyy HH:mm",
            "dd-MM-yyyy HH:mm",
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm",
            "MM/dd/yyyy",
            "M/d/yyyy"
        ];

        if (DateTime.TryParseExact(raw, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out result))
        {
            // These formats carry no timezone info — treat them as UTC.
            result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
            return true;
        }

        // RoundtripKind recognises the trailing 'Z' as UTC, preserving the time portion.
        // ToUniversalTime() normalises any offset-aware variants (e.g. +01:00) to UTC.
        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out result))
        {
            result = result.ToUniversalTime();
            return true;
        }

        result = default;
        return false;
    }
}
