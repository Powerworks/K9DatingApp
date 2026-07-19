# Inventory List — PawMatch Platform
 
## 1. Solution / Project Inventory
 
Modular monolith with one solution, one repo. Each business module is a set of projects following the same internal shape (vertical slice architecture — folders per feature/slice inside `Application`, not per technical layer).
 
```
PawMatch.sln
│
├── src/
│   ├── BuildingBlocks/
│   │   ├── PawMatch.BuildingBlocks.Domain          (base entity/aggregate, IEvent, ValueObject)
│   │   ├── PawMatch.BuildingBlocks.Messaging        (MassTransit conventions, outbox contracts)
│   │   ├── PawMatch.BuildingBlocks.Persistence      (Marten config helpers, session factory)
│   │   └── PawMatch.BuildingBlocks.Web              (minimal API conventions, ProblemDetails, auth helpers)
│   │
│   ├── Modules/
│   │   ├── Identity/
│   │   │   ├── PawMatch.Modules.Identity.Api        (module's minimal API endpoints / slices)
│   │   │   ├── PawMatch.Modules.Identity.Domain
│   │   │   ├── PawMatch.Modules.Identity.Infrastructure
│   │   │   └── PawMatch.Modules.Identity.Contracts  (public integration events/DTOs only)
│   │   │
│   │   ├── Profiles/          (dog + owner profiles, same shape as above)
│   │   ├── Discovery/         (matching/swiping, same shape)
│   │   ├── Chat/              (messaging, same shape)
│   │   ├── Notifications/     (same shape)
│   │   ├── Media/             (photo/video upload, same shape)
│   │   ├── Subscriptions/     (same shape)
│   │   └── Moderation/        (same shape)
│   │
│   ├── Host/
│   │   └── PawMatch.Api.Host                        (composition root: wires all modules, Program.cs)
│   │
│   └── Web/
│       └── PawMatch.Blazor.App                       (Blazor Web App, Interactive Server + WASM)
│
├── tests/
│   ├── PawMatch.ArchitectureTests                    (NetArchTest boundary enforcement)
│   ├── PawMatch.Modules.*.UnitTests                   (per module)
│   └── PawMatch.Modules.*.IntegrationTests             (Testcontainers-based, per module)
│
├── deploy/
│   ├── docker/                                        (Dockerfiles per deployable: Api.Host, Blazor.App)
│   ├── compose/                                        (docker-compose.yml for local dev infra)
│   ├── helm/ or bicep/terraform/                        (IaC for target platform)
│   └── github/                                          (workflow YAML, composite actions)
│
└── docs/
    └── adr/                                            (Architecture Decision Records)
```
 
## 2. NuGet Package Inventory
 
| Category | Package | Purpose |
|---|---|---|
| Document DB / Event Store | `Marten` | Document store + event sourcing on Postgres |
| Document DB | `Marten.AspNetCore` | ASP.NET Core integration helpers |
| Messaging & Mediator | `WolverineFx` | In-process command/query handling **and** message bus (replaces a separate mediator library) |
| Messaging | `WolverineFx.RabbitMQ` | RabbitMQ transport |
| Messaging + Persistence | `WolverineFx.Marten` | Wires Marten's transactional outbox/inbox directly into Wolverine — durable messaging with no custom outbox code |
| API (optional) | `WolverineFx.Http` | Lets slices expose HTTP endpoints directly from Wolverine handlers, cutting minimal-API boilerplate further |
| Caching | `StackExchange.Redis` | Redis client |
| Caching | `Microsoft.Extensions.Caching.StackExchangeRedis` | `IDistributedCache` backed by Redis |
| Real-time | `Microsoft.AspNetCore.SignalR` | Chat hub |
| Real-time scale-out | `Microsoft.AspNetCore.SignalR.StackExchangeRedis` | SignalR Redis backplane |
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT auth |
| Auth | `Microsoft.AspNetCore.Identity` (or external IdP SDK, e.g. Duende/Auth0/Entra External ID) | Identity management |
| API | `Microsoft.AspNetCore.OpenApi` / `Swashbuckle.AspNetCore` | OpenAPI generation |
| Validation | `FluentValidation` | Slice request validation |
| Mapping | `Mapster` or manual mapping | DTO ↔ domain mapping (kept minimal by design) |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly v8) | Retry/circuit breaker for outbound calls |
| Observability | `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Npgsql` | Tracing/metrics |
| Logging | `Serilog.AspNetCore`, `Serilog.Sinks.OpenTelemetry` | Structured logging |
| Health checks | `AspNetCore.HealthChecks.NpgSql`, `.Redis`, `.RabbitMQ` | Liveness/readiness probes |
| Testing | `xUnit`, `FluentAssertions`, `Testcontainers.PostgreSql`, `Testcontainers.RabbitMq`, `Testcontainers.Redis`, `NetArchTest.Rules`, `WireMock.Net` | Test suite |
| Object storage client | `Azure.Storage.Blobs` / `AWSSDK.S3` (pick per cloud) | Media storage |
| Blazor | `Microsoft.AspNetCore.Components.WebAssembly`, `Microsoft.AspNetCore.Components.Server` | Render modes |
 
> **Decision (ADR-002, confirmed):** Wolverine is the mediator + message bus for the whole solution. It acts as the in-process handler dispatcher inside a slice *and* the RabbitMQ transport across modules, using the same handler signature either way — a slice handler doesn't need to know whether it was invoked locally or over the bus. Combined with `WolverineFx.Marten`, the outbox/inbox pattern described in Section 4 of the Solution Architecture doc is "free" — Wolverine detects the `IDocumentSession` in scope and enlists outgoing messages in the same transaction automatically.
 
## 3. Infrastructure Component Inventory
 
| Component | Purpose | Local Dev | Target Platform |
|---|---|---|---|
| PostgreSQL 16+ | Marten document/event storage | Docker Compose container | Managed Postgres (Azure Flexible Server / AWS RDS / Cloud SQL) or in-cluster with persistent volume |
| RabbitMQ 3.13+ | Async integration events between modules | Docker Compose container | Managed (CloudAMQP) or self-hosted in cluster with quorum queues |
| Redis 7+ | Distributed cache, SignalR backplane, rate limiting | Docker Compose container | Managed (Azure Cache for Redis / AWS ElastiCache) |
| Object storage | Dog photos/videos | MinIO container (S3-compatible) | Azure Blob Storage / AWS S3 |
| Container registry | Store built images | local Docker | GitHub Container Registry (GHCR) or ACR/ECR |
| Orchestrator | Run containers | Docker Compose | Kubernetes (AKS/EKS/GKE) or Azure Container Apps |
| Ingress/Reverse proxy | TLS termination, routing | n/a | NGINX Ingress / Envoy / platform-native gateway |
| Secrets management | Connection strings, JWT keys | `.env` (gitignored) / user-secrets | Kubernetes Secrets + external vault (Azure Key Vault / AWS Secrets Manager) |
| Observability stack | Logs/metrics/traces | Seq or console + Jaeger container | Managed (Grafana Cloud, Azure Monitor, Datadog) or self-hosted (Prometheus/Grafana/Loki/Tempo) |
| Email provider | Notifications | Mailhog/Papercut container (dev) | SendGrid / Postmark / SES |
| Push notification provider | Mobile/web push | Provider sandbox | Firebase Cloud Messaging / OneSignal |
 
## 4. Environment Inventory
 
| Environment | Purpose | Deployed From |
|---|---|---|
| Local | Developer machine, docker-compose infra | n/a |
| CI | Ephemeral, spun up per pipeline run via Testcontainers | GitHub Actions runners |
| Dev/Integration | Shared team environment, auto-deployed on merge to `main` | GitHub Actions on push to `main` |
| Staging | Pre-prod, mirrors prod topology, used for soak/E2E tests | GitHub Actions on tag/release branch |
| Production | Live | GitHub Actions on approved release, manual gate |
 
## 5. External Integrations Inventory
 
| Integration | Used By Module | Notes |
|---|---|---|
| Geolocation/maps API (e.g., Google Maps Platform, Mapbox) | Discovery | Distance queries, geocoding of owner location |
| Email provider (SendGrid/Postmark) | Notifications | Transactional email |
| Push provider (FCM/OneSignal) | Notifications | Web push (PWA) |
| Payment provider (Stripe) — Phase 5+ | Subscriptions | Stub interface in MVP, real integration post-MVP |
| Identity provider (optional external, e.g., Entra External ID/Auth0) | Identity | Alternative to self-hosted ASP.NET Identity |
| Object storage (Blob/S3) | Media | Photo/video storage + CDN in front |
 
## 6. GitHub Actions Workflow Inventory
 
| Workflow File | Trigger | Purpose |
|---|---|---|
| `ci.yml` | PR to `main` | Restore, build, unit tests, architecture tests, lint |
| `ci-integration.yml` | PR to `main` (or nightly) | Integration tests via Testcontainers |
| `cd-dev.yml` | Push to `main` | Build & push images, deploy to Dev |
| `cd-staging.yml` | Tag `v*-rc*` | Deploy to Staging, run E2E suite |
| `cd-prod.yml` | Manual approval / release tag | Deploy to Production (blue/green or rolling) |
| `security-scan.yml` | Scheduled + PR | Dependency scanning (Dependabot/CodeQL), container image scan (Trivy) |
 