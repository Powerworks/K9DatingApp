using K9Crush.Blazor.App.Components;
using K9Crush.Blazor.App.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Persist Data Protection keys to disk - without this, ASP.NET Core
// generates a fresh in-memory key ring on every process restart, silently
// invalidating every outstanding antiforgery token and auth cookie already
// sitting in a browser (surfaces as "A valid antiforgery token was not
// provided" on the very next form submit after a restart). Single-box
// local disk storage is consistent with this app's actual deployment
// target (ADR-025 - single Hetzner VPS, not multi-instance).
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, ".dataprotection-keys")));

// UI component library - ADR-029.
builder.Services.AddMudServices();

// Auth: Supabase owns the entire signup/login/confirmation lifecycle
// directly (ADR-005) - Api.Host never issues tokens, it only validates
// Supabase-issued JWTs (see its own Program.cs JwtBearer setup). This app
// calls Supabase's own /auth/v1 REST API directly and, on success, signs
// the caller into a local auth cookie carrying the resulting Supabase JWT
// as a claim - needed later so pages can attach it as a Bearer token when
// calling our own [Authorize]-gated Api.Host endpoints.
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpClient("SupabaseAuth", client =>
{
    var supabaseUrl = builder.Configuration["Supabase:Url"]
        ?? throw new InvalidOperationException("Missing Supabase:Url configuration");
    var anonKey = builder.Configuration["Supabase:AnonKey"]
        ?? throw new InvalidOperationException("Missing Supabase:AnonKey configuration");
    client.BaseAddress = new Uri($"{supabaseUrl.TrimEnd('/')}/auth/v1/");
    client.DefaultRequestHeaders.Add("apikey", anonKey);
});
builder.Services.AddScoped<SupabaseAuthService>();
builder.Services.AddScoped<AuthorizedApiClient>();

// Typed HTTP client for the backend Api.Host - base address comes from
// config so it points at the in-cluster service name in each environment.
builder.Services.AddHttpClient("K9CrushApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
        ?? throw new InvalidOperationException("Missing ApiBaseUrl configuration"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapPost("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
