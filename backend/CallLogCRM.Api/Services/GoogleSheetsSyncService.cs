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
/// Spreadsheet columns (A–G, starting at row 2):
///   [0] Source  [1] Nom  [2] Email  [3] Telephone
///   [4] Date Rendez-Vous  [5] Closeuse  [6] Statut Call
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
        _range               = config["GoogleSheets:Range"]         ?? "A2:G";
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

        // Load all users once so we can map closer names to IDs in-memory.
        var users = await db.Users.AsNoTracking().ToListAsync(ct);
        var userMap = users
            .ToDictionary(u => u.CloserName, u => u.Id, StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var skipped  = 0;

        foreach (var row in rows)
        {
            // Guard against short / blank rows.
            if (row.Count < 6)
            {
                skipped++;
                continue;
            }

            var source       = row[0]?.ToString()?.Trim() ?? string.Empty;
            var customerName = row[1]?.ToString()?.Trim() ?? string.Empty;
            var email        = row[2]?.ToString()?.Trim() ?? string.Empty;
            var phone        = row[3]?.ToString()?.Trim() ?? string.Empty;
            var dateRaw      = row[4]?.ToString()?.Trim() ?? string.Empty;
            var closerName   = row[5]?.ToString()?.Trim() ?? string.Empty;

            // Resolve the closer → user ID.
            if (!userMap.TryGetValue(closerName, out var userId))
            {
                _logger.LogWarning(
                    "Closer '{Closer}' not found in Users table — skipping row for '{Customer}'.",
                    closerName, customerName);
                skipped++;
                continue;
            }

            // Parse the appointment date.
            if (!TryParseDate(dateRaw, out var appointmentDate))
            {
                _logger.LogWarning(
                    "Unable to parse date '{DateRaw}' — skipping row for '{Customer}'.",
                    dateRaw, customerName);
                skipped++;
                continue;
            }

            // Duplicate check: same phone + same appointment date.
            var exists = await db.CallReservations.AnyAsync(
                r => r.PhoneNumber == phone && r.AppointmentDate == appointmentDate, ct);

            if (exists)
            {
                skipped++;
                continue;
            }

            db.CallReservations.Add(new CallReservation
            {
                AssignedUserId  = userId,
                CustomerName    = customerName,
                PhoneNumber     = phone,
                Email           = email,
                AppointmentDate = appointmentDate,
                Source          = string.IsNullOrEmpty(source) ? "GoogleSheet" : source
            });

            inserted++;
        }

        if (inserted > 0)
            await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Sync result: {Inserted} inserted, {Skipped} skipped (duplicates / bad data).",
            inserted, skipped);
    }

    // ── Date parsing helper ─────────────────────────────────────

    /// <summary>
    /// Attempts to parse the date string from the sheet.  Handles common French
    /// formats (dd/MM/yyyy, dd-MM-yyyy) and ISO (yyyy-MM-dd), plus datetime variants.
    /// </summary>
    private static bool TryParseDate(string raw, out DateTime result)
    {
        // Try explicit French formats first, then fall back to generic parsing.
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
            // Normalize to UTC midnight so date comparisons are stable.
            result = DateTime.SpecifyKind(result.Date, DateTimeKind.Utc);
            return true;
        }

        // Last resort: let .NET try to figure it out.
        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result))
        {
            result = DateTime.SpecifyKind(result.Date, DateTimeKind.Utc);
            return true;
        }

        result = default;
        return false;
    }
}
