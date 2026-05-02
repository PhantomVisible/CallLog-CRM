namespace CallLogCRM.Api.DTOs;

public class ReservationAdminDto
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
