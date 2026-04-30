namespace CallLogCRM.Api.Services;

/// <summary>
/// Pushes call outcome status back to the Google Sheet.
/// </summary>
public interface IGoogleSheetsWritebackService
{
    /// <summary>
    /// Finds the matching row by phone or email and writes <paramref name="status"/>
    /// into the "Statut Call" column (G).
    /// </summary>
    Task UpdateCallStatusAsync(string phoneNumber, string email, string status);
}
