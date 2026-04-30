namespace CallLogCRM.Frontend.Models;

/// <summary>Passwordless closer selection — mirrors SelectCloserRequestDto on the backend.</summary>
public class SelectCloserRequest
{
    public string CloserName { get; set; } = string.Empty;
}

/// <summary>Admin login — mirrors AdminLoginRequestDto on the backend.</summary>
public class AdminLoginRequest
{
    public string CloserName { get; set; } = string.Empty;
    public string Password   { get; set; } = string.Empty;
}
