using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace CallLogCRM.Frontend.Services;

public class CustomAuthStateProvider(ISessionStorageService sessionStorage)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState _anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await sessionStorage.GetItemAsync<string>("authToken");
            if (string.IsNullOrWhiteSpace(token))
                return _anonymous;

            return BuildState(token);
        }
        catch
        {
            // JSInterop unavailable during static prerender — report anonymous.
            return _anonymous;
        }
    }

    public async Task MarkUserAsAuthenticated(string token)
    {
        await sessionStorage.SetItemAsync("authToken", token);
        NotifyAuthenticationStateChanged(Task.FromResult(BuildState(token)));
    }

    public async Task Logout()
    {
        await sessionStorage.RemoveItemAsync("authToken");
        NotifyAuthenticationStateChanged(Task.FromResult(_anonymous));
    }

    private static AuthenticationState BuildState(string token)
    {
        var identity = new ClaimsIdentity(ParseClaims(token), "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    // Decodes the JWT payload without verifying the signature — the backend
    // validates every protected request; the frontend only needs the claims for UI.
    private static List<Claim> ParseClaims(string token)
    {
        var payload = token.Split('.')[1];

        // Base64Url → Base64
        var padded = payload
            .PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
            .Replace('-', '+')
            .Replace('_', '/');

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .EnumerateObject()
            .Select(p => new Claim(
                // Map the short JWT claim names back to the ClaimTypes the app uses.
                p.Name switch
                {
                    "nameid"      => ClaimTypes.NameIdentifier,
                    "unique_name" => ClaimTypes.Name,
                    "role"        => ClaimTypes.Role,
                    _             => p.Name
                },
                p.Value.ToString()))
            .ToList(); // Materialize before JsonDocument is disposed.
    }
}
