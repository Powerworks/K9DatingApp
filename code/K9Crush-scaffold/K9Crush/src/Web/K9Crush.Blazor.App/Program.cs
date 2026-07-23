using K9Crush.Blazor.App.Components;
using K9Crush.Blazor.App.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
