using System.ComponentModel.DataAnnotations;
using CallLogCRM.Api.Models;

namespace CallLogCRM.Api.DTOs;

// Payload the client sends when logging a call outcome.
public class CreateCallLogDto
{
    [Required]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public CallOutcome Outcome { get; set; }

    public string?  Notes           { get; set; }
    public decimal  Revenue         { get; set; }
    public decimal  AmountCollected { get; set; }
}
