using System.ComponentModel.DataAnnotations;

namespace CallLogCRM.Api.DTOs;

public sealed class AdminLoginRequestDto
{
    [Required]
    public string CloserName { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
