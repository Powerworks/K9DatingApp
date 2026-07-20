using K9Crush.Blazor.App.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
