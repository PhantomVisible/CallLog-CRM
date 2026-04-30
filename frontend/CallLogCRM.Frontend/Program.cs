using Blazored.SessionStorage;
using CallLogCRM.Frontend.Components;
using CallLogCRM.Frontend.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Session storage (Blazored) — used to persist the JWT across navigations.
builder.Services.AddBlazoredSessionStorage();

// Auth state — reads the JWT from session storage and exposes claims to <AuthorizeView>.
// CascadingAuthenticationState is provided by the <CascadingAuthenticationState> wrapper
// in Routes.razor (NOT by AddCascadingAuthenticationState — that API creates a singleton
// cascading source incompatible with our scoped AuthenticationStateProvider).
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddAuthorizationCore();

// Typed HttpClient for the backend API.
// BaseAddress is read from "ApiBaseUrl" in appsettings.json.
builder.Services.AddHttpClient<ApiService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5055/");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
