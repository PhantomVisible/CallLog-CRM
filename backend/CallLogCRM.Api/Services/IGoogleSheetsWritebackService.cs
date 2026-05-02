namespace CallLogCRM.Api.Services;

/// <summary>
/// Pushes call outcome data back to the Google Sheet.
/// </summary>
public interface IGoogleSheetsWritebackService
{
    /// <summary>
    /// Finds the matching row by email and writes <paramref name="status"/> to column H
    /// and <paramref name="notes"/> to column I.
    /// </summary>
    Task UpdateCallStatusAsync(string phoneNumber, string email, string status, string? notes);
}
