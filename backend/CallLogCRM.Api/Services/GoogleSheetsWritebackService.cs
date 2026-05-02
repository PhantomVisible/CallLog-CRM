using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace CallLogCRM.Api.Services;

/// <summary>
/// Writes call outcome data back to the Google Sheet.
/// Finds the matching row by email address and updates the "Statut Call" column (G).
/// </summary>
public sealed class GoogleSheetsWritebackService(
    IConfiguration config,
    ILogger<GoogleSheetsWritebackService> logger) : IGoogleSheetsWritebackService
{
    private readonly string _spreadsheetId   = config["GoogleSheets:SpreadsheetId"] ?? string.Empty;
    private readonly string _credentialsPath = config["GoogleSheets:CredentialsPath"] ?? "google-credentials.json";

    /// <inheritdoc />
    public async Task UpdateCallStatusAsync(string phoneNumber, string email, string status, string? notes)
    {
        try
        {
            var sheetsService = await CreateSheetsServiceAsync();

            // 1. Fetch all rows — range A2:H captures all data columns including Statut Call.
            var getRequest = sheetsService.Spreadsheets.Values.Get(_spreadsheetId, "A2:H");
            var getResponse = await getRequest.ExecuteAsync();
            var rows = getResponse.Values;

            if (string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning("No email provided — cannot write back status for phone={Phone}.", phoneNumber);
                return;
            }

            if (rows is null || rows.Count == 0)
            {
                logger.LogWarning("Sheet is empty — cannot write back status for email={Email}.", email);
                return;
            }

            // 2. Find the matching row index (0-based within the fetched data, so +2 for the sheet row).
            //    Actual column layout (A2:H):
            //      [0]=A(leading) [1]=Source [2]=Nom [3]=Email [4]=Telephone [5]=unused [6]=Date [7]=Closer
            //    Email is Column D = index 3.
            const int emailColumnIndex = 3;

            // Aggressively strip invisible characters that can survive a copy-paste from Sheets.
            var targetEmail = email.Replace("\r", "").Replace("\n", "").Trim();

            logger.LogInformation("Writeback search starting — target email=[{Target}], {Count} rows fetched.",
                targetEmail, rows.Count);

            int? matchedRowIndex = null;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Count <= emailColumnIndex)
                {
                    logger.LogDebug("Row {I} skipped — only {Cols} column(s).", i, rows[i].Count);
                    continue;
                }

                var rawCell   = rows[i][emailColumnIndex]?.ToString() ?? string.Empty;
                var sheetEmail = rawCell.Replace("\r", "").Replace("\n", "").Trim();

                logger.LogDebug("Row {I}: sheet=[{SheetEmail}] target=[{TargetEmail}]",
                    i, sheetEmail, targetEmail);

                if (sheetEmail.Equals(targetEmail, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Writeback match found at row {I} (sheet row {SheetRow}).",
                        i, i + 2);
                    matchedRowIndex = i;
                    break;
                }
            }

            if (matchedRowIndex is null)
            {
                logger.LogWarning(
                    "Writeback FAILED — no row matched email=[{Email}].", targetEmail);
                return;
            }

            // 3. Write status → column H and notes → column I of the matched row.
            //    Sheet rows are 1-indexed and we skip the header row, so: sheetRow = matchedRowIndex + 2.
            var sheetRow    = matchedRowIndex.Value + 2;
            var updateRange = $"H{sheetRow}:I{sheetRow}";

            var valueRange = new ValueRange
            {
                Values = [[(object)status, notes ?? ""]]
            };

            var updateRequest = sheetsService.Spreadsheets.Values.Update(
                valueRange, _spreadsheetId, updateRange);
            updateRequest.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            logger.LogInformation(
                "Attempting to write Status=[{Status}] Notes=[{Notes}] to {Range}.",
                status, notes, updateRange);

            await updateRequest.ExecuteAsync();

            logger.LogInformation(
                "Successfully updated {Range} (email={Email}).",
                updateRange, email);
        }
        catch (Exception ex)
        {
            // Never let a sheet-write failure break the main call-log flow.
            logger.LogError(ex,
                "Failed to write back status to Google Sheet for email={Email}.", email);
        }
    }

    private async Task<SheetsService> CreateSheetsServiceAsync()
    {
        GoogleCredential credential;
        await using (var stream = File.OpenRead(_credentialsPath))
        {
#pragma warning disable CS0618
            credential = (await GoogleCredential.FromStreamAsync(stream, CancellationToken.None))
                .CreateScoped(SheetsService.Scope.Spreadsheets);
#pragma warning restore CS0618
        }

        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName       = "CallLogCRM-Writeback"
        });
    }
}
