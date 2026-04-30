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
}
