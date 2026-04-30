namespace CallLogCRM.Api.Models;

public class User
{
    public Guid    Id           { get; set; } = Guid.NewGuid();
    public string? Email        { get; set; }
    public string? PasswordHash { get; set; }

    // "Closer" or "Admin"
    public string Role       { get; set; } = "Closer";

    // Primary identifier — matches the display name in the Google Sheet sync.
    public string CloserName { get; set; } = string.Empty;

    public ICollection<CallLog>         CallLogs         { get; set; } = [];
    public ICollection<CallReservation> CallReservations { get; set; } = [];
}
