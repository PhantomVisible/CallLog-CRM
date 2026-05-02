namespace CallLogCRM.Api.DTOs;

public class CallLogAdminDto
{
    public Guid     Id           { get; set; }
    public string   CloserName   { get; set; } = string.Empty;
    public string   CustomerName { get; set; } = string.Empty;
    public string   PhoneNumber  { get; set; } = string.Empty;
    public string   Outcome      { get; set; } = string.Empty;
    public string?  Notes           { get; set; }
    public decimal  Revenue         { get; set; }
    public decimal  AmountCollected { get; set; }
    public DateTime CreatedAt       { get; set; }
}
