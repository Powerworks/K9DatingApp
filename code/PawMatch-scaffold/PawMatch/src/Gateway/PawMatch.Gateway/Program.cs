using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Routes/clusters are config-driven (appsettings.json "ReverseProxy"
// section) rather than code-first, so environment-specific upstream
// addresses (in-cluster service DNS names) are just config, not a
// rebuild. See appsettings.*.json for the actual route table.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Centralizing rate limits here (rather than in Api.Host) means the limit
// applies the same way regardless of which upstream eventually serves a
// given module - and if a module is later split into its own service,
// this policy doesn't need to move.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("swipe-endpoint", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            }));
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseRateLimiter();
app.MapHealthChecks("/healthz/live");
app.MapReverseProxy();

app.Run();
