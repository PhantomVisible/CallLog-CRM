using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace CallLogCRM.Api.Services;

/// <summary>
/// Writes call outcome data back to the Google Sheet.
/// Finds the matching row by phone number and updates the "Statut Call" column (G).
/// </summary>
public sealed class GoogleSheetsWritebackService(
    IConfiguration config,
    ILogger<GoogleSheetsWritebackService> logger) : IGoogleSheetsWritebackService
{
    private readonly string _spreadsheetId   = config["GoogleSheets:SpreadsheetId"] ?? string.Empty;
    private readonly string _credentialsPath = config["GoogleSheets:CredentialsPath"] ?? "google-credentials.json";

    /// <inheritdoc />
    public async Task UpdateCallStatusAsync(string phoneNumber, string email, string status)
    {
        try
        {
            var sheetsService = await CreateSheetsServiceAsync();

            // 1. Fetch all rows to find the one matching this phone/email.
            var getRequest = sheetsService.Spreadsheets.Values.Get(_spreadsheetId, "A2:G");
            var getResponse = await getRequest.ExecuteAsync();
            var rows = getResponse.Values;

            if (rows is null || rows.Count == 0)
            {
                logger.LogWarning("Sheet is empty — cannot write back status for {Phone}.", phoneNumber);
                return;
            }

            // 2. Find the matching row index (0-based within the fetched data, so +2 for the sheet row).
            int? matchedRowIndex = null;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Count < 4) continue;

                var rowPhone = rows[i][3]?.ToString()?.Trim() ?? string.Empty;
                var rowEmail = rows[i][2]?.ToString()?.Trim() ?? string.Empty;

                // Match by phone first; fall back to email if phone is empty.
                if (!string.IsNullOrWhiteSpace(phoneNumber) &&
                    string.Equals(rowPhone, phoneNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    matchedRowIndex = i;
                    break;
                }

                if (!string.IsNullOrWhiteSpace(email) &&
                    string.Equals(rowEmail, email.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    matchedRowIndex = i;
                    break;
                }
            }

            if (matchedRowIndex is null)
            {
                logger.LogWarning(
                    "No matching row found in sheet for phone={Phone}, email={Email}.",
                    phoneNumber, email);
                return;
            }

            // 3. Write the status into column G (index 6) of the matched row.
            //    Sheet rows are 1-indexed and we skip the header row, so: sheetRow = matchedRowIndex + 2.
            var sheetRow = matchedRowIndex.Value + 2;
            var updateRange = $"G{sheetRow}";

            var valueRange = new ValueRange
            {
                Values = [[status]]
            };

            var updateRequest = sheetsService.Spreadsheets.Values.Update(
                valueRange, _spreadsheetId, updateRange);
            updateRequest.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await updateRequest.ExecuteAsync();

            logger.LogInformation(
                "Updated sheet row {Row} (phone={Phone}) with status '{Status}'.",
                sheetRow, phoneNumber, status);
        }
        catch (Exception ex)
        {
            // Never let a sheet-write failure break the main call-log flow.
            logger.LogError(ex,
                "Failed to write back status to Google Sheet for phone={Phone}.", phoneNumber);
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
