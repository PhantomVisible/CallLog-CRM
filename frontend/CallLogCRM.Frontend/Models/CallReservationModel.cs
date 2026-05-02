namespace CallLogCRM.Frontend.Models;

/// <summary>
/// Mirrors the backend CallReservation entity.
/// Returned by GET /api/reservations/mine and GET /api/reservations/{id}.
/// </summary>
public class CallReservation
{
    public Guid     Id              { get; set; }
    public Guid     AssignedUserId  { get; set; }
    public string   CustomerName    { get; set; } = string.Empty;
    public string   PhoneNumber     { get; set; } = string.Empty;
    public string   Email           { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public string   Source          { get; set; } = string.Empty;

    // Live status from the Google Sheet "Statut Call" column (H).
    public string?  CurrentStatus   { get; set; }
}

// Admin view — returned by GET /api/reservations/all, includes CloserName.
public class AdminReservation
{
    public Guid     Id              { get; set; }
    public string   CloserName      { get; set; } = string.Empty;
    public string   CustomerName    { get; set; } = string.Empty;
    public string   PhoneNumber     { get; set; } = string.Empty;
    public string   Email           { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public string   Source          { get; set; } = string.Empty;
    public string?  CurrentStatus   { get; set; }
    public string?  Notes           { get; set; }
}
