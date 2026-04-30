using System.ComponentModel.DataAnnotations;

namespace CallLogCRM.Api.DTOs;

public sealed class SelectCloserRequestDto
{
    [Required]
    public string CloserName { get; init; } = string.Empty;
}
