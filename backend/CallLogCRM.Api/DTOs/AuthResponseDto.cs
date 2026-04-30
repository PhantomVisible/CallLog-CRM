namespace CallLogCRM.Api.DTOs;

public sealed class AuthResponseDto
{
    public string Token      { get; init; } = string.Empty;
    public string CloserName { get; init; } = string.Empty;
}
