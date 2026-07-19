# PawMatch

.NET modular monolith backend (vertical slice architecture, Marten + Wolverine on PostgreSQL/RabbitMQ, Redis) with a Blazor Web App frontend, YARP gateway, and Supabase Cloud for identity, Postgres, and object storage.

## Documents
See `docs/` for the full design set:
- `01-project-plan.md`
- `02-inventory-list.md`
- `03-solution-architecture.md`
- `04-high-level-design.md`
- `05-event-modeling-blueprint.md` — **read this first** if you're adding a new slice; it defines the state-change / state-view / automation discipline every module follows.

## Status of this scaffold
- `PawMatch.Modules.Profiles.*` — CRUD/document-style module, fully wired (CreateDogProfile command, GetDogProfile read model).
- `PawMatch.Modules.Discovery.*` — event-sourced module, fully wired (SwipeOnDog command, DetectMutualMatch automation, GetDiscoveryFeed read model).
- `PawMatch.Api.Host` — composition root: Marten, Wolverine (RabbitMQ + Marten outbox/inbox + event forwarding), Redis, Supabase JWT auth, health checks.
- `PawMatch.Gateway` — YARP reverse proxy with centralized rate limiting.
- `PawMatch.Blazor.App` — Blazor Web App starter (Interactive Server render mode), calls the API through a typed HttpClient.
- Not yet scaffolded: Identity, Chat, Notifications, Media, Subscriptions, Moderation modules; `PawMatch.ArchitectureTests`; GitHub Actions workflows; the ADR-017 role-lookup table.
- Dockerfiles and a production-style `docker-compose.prod.yml` exist under `deploy/` for a single-host MVP deployment - see `GETTING_STARTED.md`. Kubernetes/Helm (ADR-006/011) is the later-scale target, not built yet.

## Building this locally
This scaffold was generated without network access to NuGet, so packages have **not** been restored or built. Package versions in `Directory.Packages.props` use floating (`X.*`) versions — pin them to exact versions on first `dotnet restore` and commit the lock.

```bash
dotnet restore
dotnet build
```

You'll also need RabbitMQ and Redis running locally (`deploy/compose/docker-compose.yml`), plus a Supabase Cloud project for Postgres/Storage/Auth (see `GETTING_STARTED.md`) — Postgres and object storage are Supabase-managed now, not local containers.
