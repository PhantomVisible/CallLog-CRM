using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.SessionStorage;
using CallLogCRM.Frontend.Models;

namespace CallLogCRM.Frontend.Services;

// Thin wrapper around HttpClient for all backend API calls.
// Registered as a typed client in Program.cs — HttpClient base address is configured there.
public class ApiService
{
    private readonly HttpClient _http;
    private readonly ISessionStorageService _session;

    public ApiService(HttpClient http, ISessionStorageService session)
    {
        _http    = http;
        _session = session;
    }

    // ── Auth ──────────────────────────────────────────────

    /// <summary>Passwordless closer login (POST /api/auth/select-closer).</summary>
    public async Task<AuthResponse?> SelectCloserAsync(string closerName)
    {
        var response = await _http.PostAsJsonAsync(
            "api/auth/select-closer",
            new SelectCloserRequest { CloserName = closerName });

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    /// <summary>Admin login with password (POST /api/auth/admin-login).</summary>
    public async Task<AuthResponse?> AdminLoginAsync(string closerName, string password)
    {
        var response = await _http.PostAsJsonAsync(
            "api/auth/admin-login",
            new AdminLoginRequest { CloserName = closerName, Password = password });

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    // ── Reservations ─────────────────────────────────────

    /// <summary>GET /api/reservations/mine — all reservations for the authenticated closer.</summary>
    public async Task<List<CallReservation>> GetMyReservationsAsync()
    {
        await AttachTokenAsync();
        return await _http.GetFromJsonAsync<List<CallReservation>>("api/reservations/mine") ?? [];
    }

    /// <summary>GET /api/reservations/{id} — single reservation details.</summary>
    public async Task<CallReservation?> GetReservationAsync(Guid id)
    {
        await AttachTokenAsync();
        var response = await _http.GetAsync($"api/reservations/{id}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<CallReservation>();
    }

    // ── Call Logs ─────────────────────────────────────────

    // GET /api/calllogs
    public async Task<List<CallLog>> GetCallLogsAsync()
    {
        await AttachTokenAsync();
        return await _http.GetFromJsonAsync<List<CallLog>>("api/calllogs") ?? [];
    }

    // POST /api/calllogs
    // Returns true on 2xx, throws on network failure so the caller can show an error.
    public async Task<bool> CreateCallLogAsync(CreateCallLogRequest request)
    {
        await AttachTokenAsync();
        var response = await _http.PostAsJsonAsync("api/calllogs", request);
        return response.IsSuccessStatusCode;
    }

    // ── Private Helpers ──────────────────────────────────

    /// <summary>
    /// Reads the JWT from session storage and sets the Authorization header.
    /// Safe to call multiple times — replaces the header each time.
    /// </summary>
    private async Task AttachTokenAsync()
    {
        try
        {
            var token = await _session.GetItemAsync<string>("authToken");
            _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
                ? null
                : new AuthenticationHeaderValue("Bearer", token);
        }
        catch
        {
            // Session storage unavailable (e.g. during prerender) — leave header as-is.
        }
    }
}
