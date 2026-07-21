using System.Reflection;
using Marten;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using K9Crush.BuildingBlocks.Domain;
using K9Crush.BuildingBlocks.Persistence;
using K9Crush.BuildingBlocks.Web;
using K9Crush.Modules.Admin.Api;
using K9Crush.Modules.Chat.Api;
using K9Crush.Modules.Discovery.Api;
using K9Crush.Modules.Identity.Api;
using K9Crush.Modules.Media.Api;
using K9Crush.Modules.Notifications.Api;
using K9Crush.Modules.Profiles.Api;
using K9Crush.Modules.ShelterAdoption.Api;
using Serilog;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Marten;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// --- Logging -----------------------------------------------------------
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// --- Module discovery ----------------------------------------------------
// This is the one place in the whole solution that knows the full list of
// business modules. Adding a module means adding one line here and a
// ProjectReference in this csproj - nothing else changes.
var modules = new IModule[]
{
    new IdentityModule(),
    new ProfilesModule(),
    new DiscoveryModule(),
    new ShelterAdoptionModule(),
    new NotificationsModule(),
    new ChatModule(),
    new AdminModule(),
    new MediaModule()
};

foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

// --- Marten (one DocumentStore, one schema per module - ADR-003) --------
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");

builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    options.ApplyModuleConfigurations(modules.Select(m => m.MartenConfiguration));

    // NOTE: the explicit AutoCreateSchemaObjects assignment that used to
    // be here (Development -> CreateOrUpdate, else -> None) has been
    // removed rather than guessed at. Marten 9's "Critter Stack 2026"
    // release restructured this exact setting as part of a broader
    // "unified resource model" shared with Wolverine (search turned up
    // references to CritterStackDefaults / ResourceAutoCreate / a new
    // IServiceCollection.AddJasperFx() configuration surface, but not a
    // definitive current namespace for the old Weasel.Core.AutoCreate
    // enum specifically - guessing wrong here risks silently disabling
    // schema creation rather than a compile error).
    //
    // What this means right now: Marten's own default is CreateOrUpdate
    // (confirmed via its test suite), which is what Development needs -
    // so local dev should work unchanged with no explicit setting.
    // Before deploying anywhere beyond local dev, this needs deliberate
    // configuration (likely via AddJasperFx() per the pattern above) so
    // schema auto-creation is explicitly OFF outside Development - don't
    // ship without resolving this. Your IDE's "go to definition" on
    // AddJasperFx or CritterStackDefaults (once you add a `using JasperFx;`
    // and start typing) will show you the actual current API against the
    // exact package version that's actually installed, which is more
    // reliable than what I can confirm from documentation alone.
})
.IntegrateWithWolverine(m =>
{
    // Forwards captured Marten domain events to any local Wolverine
    // handler for that event type - this is what makes automation
    // slices (EVENT -> AUTOMATION -> COMMAND -> EVENT) work without a
    // hand-rolled polling loop. Each automation just declares a
    // Handle(TDomainEvent) method; Wolverine finds and invokes it.
    // See K9Crush.Modules.Discovery.Api.Automations.DetectMutualMatch
    // for the concrete example (reacts to DogLiked).
    //
    // Verify this call against the installed WolverineFx.Marten version
    // - event forwarding vs. the newer async-daemon event-subscriptions
    // API have both existed at different points; pick one per the
    // library's current guidance and don't mix both in the same app.
    m.SubscribeToEvent<K9Crush.Modules.Discovery.Domain.Events.DogLiked>();

    // Chat's own read-model projectors (ReadModels/Projectors) - same
    // forwarding mechanism, see ChatModule.cs.
    m.SubscribeToEvent<K9Crush.Modules.Chat.Domain.Events.ConversationCreated>();
    m.SubscribeToEvent<K9Crush.Modules.Chat.Domain.Events.MessageSent>();
});

// --- Wolverine (mediator + RabbitMQ transport + Http endpoints) ---------
var rabbitConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:RabbitMQ");

builder.Host.UseWolverine(opts =>
{
    // Handlers are discovered from every module assembly automatically -
    // no per-module registration call needed beyond referencing the
    // assembly (which Api.Host already does via ProjectReference).
    foreach (var module in modules)
    {
        opts.Discovery.IncludeAssembly(module.GetType().Assembly);
    }

    opts.UseRabbitMq(new Uri(rabbitConnectionString)).AutoProvision();

    // Integration events route through the shared topic exchange
    // described in the Solution Architecture doc, Section 5. Each
    // consumer module gets its own durable queue bound to the events it
    // handles - Wolverine infers routing from the message type by
    // convention here; override per-message-type as needed.
    opts.PublishAllMessages().ToRabbitExchange("k9crush.events");

    // This comment described the intent above, but nothing actually
    // implemented the receiving half until now: PublishAllMessages(...)
    // is publish-only, so every integration event was published into
    // k9crush.events and dropped - confirmed live (see IModule.cs's
    // IntegrationEventQueueName doc comment for the reproduction). Each
    // module that declares a queue name gets it bound to the exchange
    // here; Wolverine's own message-type dispatch then routes each
    // delivered event to whichever local Handle(TEvent) matches, same as
    // if it had arrived in-process.
    foreach (var module in modules)
    {
        if (module.IntegrationEventQueueName is { } queueName)
        {
            opts.ListenToRabbitQueue(queueName, queue => queue.BindExchange("k9crush.events"));
        }
    }

    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
    opts.Policies.UseDurableInboxOnAllListeners();

    opts.Policies.OnException<Exception>()
        .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))
        .Then.MoveToErrorQueue();
});

builder.Services.AddWolverineHttp();

// --- Redis: distributed cache + SignalR backplane ------------------------
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis");

builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnectionString);

builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString);

// --- API infrastructure --------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// --- Auth: validate JWTs issued by Supabase Auth (ADR-005) ---------------
// Api.Host is a pure resource server - it never issues or stores
// credentials. Supabase Cloud owns registration/login/MFA/password reset;
// this only validates the bearer token Supabase already issued.
//
// Unlike Keycloak, Supabase's default token signing is a shared HS256
// secret, not OIDC-discovery-compatible JWKS - so this uses an explicit
// SymmetricSecurityKey rather than the options.Authority auto-discovery
// pattern Keycloak supported. Supabase does offer a newer asymmetric
// (ES256/JWKS) signing mode, which is the better long-term fit (no shared
// secret living in this config at all), but it requires explicitly
// enabling it in the Supabase project first - not done here. Revisit once
// that's turned on: swap this for TokenValidationParameters.IssuerSigningKeyResolver
// fetching https://<project-ref>.supabase.co/auth/v1/.well-known/jwks.json.
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Missing Supabase:Url");
var supabaseJwtSecret = builder.Configuration["Supabase:JwtSecret"]
    ?? throw new InvalidOperationException("Missing Supabase:JwtSecret");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateAudience = true,
            // "authenticated" is Supabase's standard audience for user
            // session tokens - a fixed string, not project-specific.
            // Unverified against a real token this session; confirm
            // against your actual Supabase project's issued JWTs.
            ValidAudience = "authenticated",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(supabaseJwtSecret)),
            ValidateLifetime = true
        };

        // Since .NET 8, JwtBearer's default token handler stopped
        // auto-remapping short JWT claim names (sub, email, name, ...) to
        // their long-form ClaimTypes.* URIs - claims come through exactly
        // as the IdP names them instead. Every handler in this codebase
        // that reads ClaimTypes.NameIdentifier (e.g. CreateDogProfile,
        // SwipeOnDog) was written assuming the older remapped behavior.
        // Restoring it centrally here means those handlers don't each
        // need to know the IdP's raw claim names - fix once, works
        // everywhere any future module reads the caller's identity. This
        // was originally fixed for Keycloak but applies identically to
        // Supabase, since both issue standard JWTs with short claim names.
        options.MapInboundClaims = true;
    });

// ADR-017 role checking (RoleRequirement/RoleAuthorizationHandler, both
// in BuildingBlocks.Web) - registered Scoped, not Singleton, since its
// IOwnerRoleLookup dependency is itself Scoped (backed by Marten's
// IQuerySession). See RoleRequirement.cs's doc comment.
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, RoleAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("VerifiedOwner", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("email_verified", "true"));

    // Reviewer-only actions on someone else's ShelterAccount (verify,
    // activate, flag/approve/reject) - see ShelterAdoption's Commands/*
    // handlers, all originally flagged as having no role check at all.
    options.AddPolicy("Admin", policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim("email_verified", "true")
            .AddRequirements(new RoleRequirement(OwnerRole.Admin)));

    // Actions on a shelter's own resources once it's been activated
    // (e.g. AddDogListing) - combined with an in-handler ownership check,
    // since a Shelter-role caller should still only manage their own
    // ShelterAccount's resources, not every shelter's.
    options.AddPolicy("Shelter", policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim("email_verified", "true")
            .AddRequirements(new RoleRequirement(OwnerRole.Shelter)));
});

// AspNetCore.HealthChecks.Rabbitmq 9.x changed AddRabbitMQ() to no longer
// create its own connection internally - it now resolves a RabbitMQ.Client
// IConnection from DI and expects the caller to register one. Confirmed
// against the package's own current NuGet README (this was already
// flagged as the highest-risk version pin in this file, and this is
// exactly the kind of break that risk was about). Registered as a
// singleton per the client's own guidance (connections are meant to be
// long-lived, not created per health check).
builder.Services.AddSingleton<RabbitMQ.Client.IConnection>(_ =>
{
    var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitConnectionString) };
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres")
    .AddRedis(redisConnectionString, name: "redis")
    .AddRabbitMQ(name: "rabbitmq");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapWolverineEndpoints(opts =>
{
    // Wolverine.Http endpoints bypass the message-bus pipeline entirely,
    // so WolverineFx.FluentValidation (which only hooks into
    // IMessageBus.InvokeAsync/SendAsync) never runs for [WolverineGet]/
    // [WolverinePost] handlers - confirmed via reflection against the
    // real 6.17.2 assemblies after an invalid RequestShelterAccount body
    // 500'd instead of 400ing. This is Wolverine.Http's own, separate
    // validation subsystem: it runs System.ComponentModel.DataAnnotations
    // (attributes + IValidatableObject) against the request DTO before
    // the handler runs, and short-circuits with a 400 ProblemDetails on
    // failure. Every command request record's validation now lives on
    // the record itself (attributes, or IValidatableObject for
    // cross-field/Guid-not-empty checks that plain attributes can't
    // express) instead of a separate AbstractValidator<T> class - see
    // CreateDogProfileRequest, SwipeOnDogRequest, RequestShelterAccountRequest.
    opts.UseDataAnnotationsValidationProblemDetailMiddleware();
}); // maps every [WolverineGet]/[WolverinePost] slice across all modules

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.MapHealthChecks("/healthz/live");
app.MapHealthChecks("/healthz/ready");

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
