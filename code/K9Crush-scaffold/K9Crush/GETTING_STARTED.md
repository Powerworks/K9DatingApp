# Getting Started — Running K9Crush Locally

This scaffold has never been restored, built, or run (it was generated without NuGet/network access). Treat this as a strong starting point, not verified-working code — Section 5 below lists specifically where it's most likely to break and why.

## Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — **not .NET 8.** The solution originally targeted .NET 8, but current Marten (9.x) and Wolverine (6.x) have dropped net8.0 support entirely; see ADR-020 in the Solution Architecture doc. `global.json` pins the SDK to `10.0.100`.
- Docker Desktop (or equivalent) for the local infra containers
- `dotnet --version` should print `10.0.1xx`. If you have both .NET 8 and .NET 10 SDKs installed side by side, that's fine — `global.json`'s `rollForward: latestFeature` will pick the right one for this repo specifically without affecting other projects on your machine.

## 1. Start local infrastructure

```bash
docker compose -f deploy/compose/docker-compose.yml up -d
```

This brings up RabbitMQ, Redis, smtp4dev, and the Grafana LGTM-in-one-container image — no Postgres, no object storage, no auth container locally. Postgres, Storage, and Auth are all Supabase-managed now (ADR-005/024), set up in Step 2 below. Give it a minute — check everything's healthy:

```bash
docker compose -f deploy/compose/docker-compose.yml ps
```

Useful UIs once it's up:
| Service | URL | Login |
|---|---|---|
| RabbitMQ management | http://localhost:15672 | guest / guest |
| Grafana | http://localhost:3000 | admin / admin |
| smtp4dev (email inbox) | http://localhost:5080 | none |

## 2. Set up the Supabase project (manual, one-time)

Supabase Cloud covers Auth, Postgres, and Storage now (ADR-005/024) — all external managed services, nothing local to run for any of them.

1. Go to https://supabase.com, create a free account if you don't have one, and create a new project (pick any name/region - this is throwaway for local dev).
2. Wait for provisioning to finish (a couple of minutes).
3. **Project Settings → API** → copy the **Project URL** (`https://<project-ref>.supabase.co`) into `Supabase:Url` in `appsettings.Development.json`.
4. **Project Settings → API → JWT Settings** → copy the **JWT Secret** into `Supabase:JwtSecret` in the same file. Treat this like any other secret - it's already `.gitignore`d as part of `appsettings.*.local.json`-style patterns, but double-check before pushing if you fork this repo.
5. **Project Settings → Database → Connection string.** This is the step most likely to bite you: Supabase shows multiple connection string variants (Direct connection, Session pooler, Transaction pooler). **Use "Session pooler" or "Direct connection" — never "Transaction pooler."** Marten's async daemon relies on Postgres advisory locks for leader election, and transaction-mode pooling doesn't reliably support session-level features like those. Getting this wrong doesn't fail loudly — the app will likely start fine and only misbehave subtly around projection/subscription processing. Copy that connection string's host/port/password into `ConnectionStrings:Postgres` in `appsettings.Development.json` (Npgsql connection string format — you may need to reformat from the `postgres://` URL Supabase shows into `Host=...;Port=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true`).
6. **Database → Extensions** → enable `postgis` (ADR-014 needs this from day one for Discovery/Places/Lost & Found's proximity queries — Marten won't enable it for you).
7. **Authentication → Users → Add user** → create a test owner account with an email/password, and confirm the email (Supabase's dashboard lets you manually confirm a test user without actually receiving an email).
8. To get a token for `curl` testing (no Blazor OIDC flow wired up yet - see Section 6):
   ```bash
   curl -s -X POST "https://<project-ref>.supabase.co/auth/v1/token?grant_type=password" \
     -H "apikey: <your project's anon/public key, from Project Settings -> API>" \
     -H "Content-Type: application/json" \
     -d '{"email":"<your test user email>","password":"<their password>"}' \
     | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])"
   ```
   Unverified against a live project this session - if the response shape differs from what's shown here, check Supabase's current Auth API docs rather than assuming this is exactly right.

## 3. Create the Supabase Storage bucket

Media module isn't scaffolded yet, so nothing needs this today, but it's a 30-second step to have ready:
1. Supabase dashboard → **Storage** → **New bucket** → `k9crush-media`.
2. If/when the Media module gets built, its S3-compatible client config points at `https://<project-ref>.supabase.co/storage/v1/s3` with an access key/secret generated from Storage settings — not the same credentials as `Supabase:JwtSecret` above.

## 4. Restore and build

```bash
cd K9Crush
dotnet restore
dotnet build
```

`Directory.Packages.props` uses floating versions (`7.*`, `2.*`, etc.) since I couldn't reach NuGet to pin exact versions when generating this. **Expect `dotnet restore` to pull whatever the current latest patch is** — if `dotnet build` then fails on an API mismatch, that's the most likely first cause: check what actually got restored (`dotnet list package`) against what the code assumes, and pin exact versions in `Directory.Packages.props` once you know what works.

## 5. Run it

Three separate terminals (or run configs in your IDE):

```bash
# Terminal 1
dotnet run --project src/Host/K9Crush.Api.Host
# → http://localhost:5100/swagger

# Terminal 2
dotnet run --project src/Web/K9Crush.Blazor.App
# → http://localhost:5200

# Terminal 3 (optional at this stage - you can hit Api.Host/Blazor.App directly first)
dotnet run --project src/Gateway/K9Crush.Gateway
```

**Start with just Api.Host.** Get that compiling and serving Swagger before layering on Blazor.App and Gateway — it's the piece with the most moving parts (Marten, Wolverine, Redis, Supabase auth all wire up in one `Program.cs`).

Quick smoke test once Api.Host is running:
```bash
curl http://localhost:5100/healthz/live
curl -X POST http://localhost:5100/api/v1/profiles/dogs -H "Content-Type: application/json" \
  -d '{"name":"Rex","breed":"Labrador","ageInMonths":24,"bio":"Good boy","latitude":53.35,"longitude":-6.26}'
```
That second call will 401 — `CreateDogProfileHandler` requires an authenticated `ClaimsPrincipal` and there's no login flow wired up yet (Section 6). Getting a 401 rather than a 500 is actually the useful signal here — it means the app started and auth middleware is running.

## 6. What's deliberately not done yet (don't be surprised)
- **smtp4dev is running but nothing sends email to it yet** - the Notifications module (which would actually configure `MailKit`/`SmtpClient` against `localhost:2525` and send real messages) isn't scaffolded yet. The container's ready and waiting; there's just no code exercising it.
- **Supabase JWT validation uses a shared HS256 secret, not JWKS/OIDC discovery.** This is Supabase's default (simpler, but means the secret lives in `appsettings.Development.json` - fine for local dev, not for anything beyond it). Supabase's newer asymmetric (ES256/JWKS) signing mode is the better long-term fit and avoids that shared-secret exposure entirely, but requires explicitly enabling it in the Supabase project dashboard first - not done here. See the comment in `Program.cs`'s auth section for what changes when you switch.
- **The `ValidIssuer`/`ValidAudience` values in `Program.cs` (`{supabaseUrl}/auth/v1` and `"authenticated"`) are unverified against a real Supabase-issued token this session.** If you get an issuer or audience validation failure, decode your actual token (jwt.io or similar) and compare its `iss`/`aud` claims against what's configured - these are standard/documented Supabase conventions but haven't been confirmed against a live project.
- **`GetDiscoveryFeedHandler` and `GetDogProfileHandler` have no `[Authorize]`.** Unlike `CreateDogProfile` and `SwipeOnDog` (both fixed to require auth after real bugs surfaced there), these two are read-only `GET` endpoints that never touch `ClaimsPrincipal`, so they don't crash - but whether browsing the discovery feed or a dog's profile should require login is a product decision, not something I should silently pick for you. Left public for now; revisit deliberately.
- **Wolverine's handler code is dynamically compiled at runtime (`WolverineFx.RuntimeCompilation`), not pre-generated.** This is the simpler path and fine for local dev, but production should switch to static codegen (`dotnet run -- codegen write` as a CI step, committing/building the generated code, then `opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Static` in `Program.cs`) for faster cold starts and to avoid shipping a Roslyn dependency in the container image. Not yet done - there's no CI step for it and `Program.cs` doesn't set `TypeLoadMode`. Do this before/alongside building the actual Dockerfiles, not as an afterthought.
- **Marten schema auto-create is not explicitly disabled for non-Development environments.** Marten 9's underlying `AutoCreate` configuration got restructured as part of a broader Critter Stack refactor and I couldn't confirm its current namespace/API confidently enough to guess at in `Program.cs` (see the comment there). Local dev relies on Marten's own default (`CreateOrUpdate`), which is fine for local. **Do not deploy this anywhere beyond local dev until this is explicitly resolved** — letting schema auto-create run in a real environment is the kind of thing that's fine until it silently isn't. This now matters slightly differently than it used to: your "local dev" Postgres is a real Supabase Cloud project (ADR-024), not a disposable local container, so schema auto-create is running against real cloud infrastructure from the start, not localhost.
- **No login flow in Blazor.App** — `Program.cs` doesn't have Supabase auth wired up yet, only the typed HttpClient to the API. You can't get a real JWT through the UI yet; testing authenticated endpoints for now means getting a token directly from Supabase's token endpoint with `curl`/Postman per Step 2.
- **Role lookup table doesn't exist yet** — ADR-017 calls for `Api.Host` to resolve Owner/Vendor/Shelter/Admin roles via a Postgres lookup keyed by the caller's `sub` claim, but that table and the lookup code aren't built. Every authenticated request right now is implicitly "Owner" by virtue of the `VerifiedOwner` policy; there's no actual role differentiation in code yet.
- **`K9Crush.ArchitectureTests` doesn't exist yet** — the fitness-test rules described in the Event Modeling blueprint (Section 6) are written down but not implemented as actual NetArchTest code.
- **No unit/integration tests at all yet** — `tests/` isn't scaffolded.
- **Dockerfiles exist now** (`deploy/docker/*.Dockerfile`) but are unbuilt/untested this session - same caveat as everything else in this scaffold. Build them yourself before trusting them:
  ```bash
  docker build -f deploy/docker/ApiHost.Dockerfile -t k9crush-api-host .
  docker build -f deploy/docker/Gateway.Dockerfile -t k9crush-gateway .
  docker build -f deploy/docker/BlazorApp.Dockerfile -t k9crush-blazor-app .
  ```
  Or build and run the whole MVP stack at once - see `deploy/compose/docker-compose.prod.yml` and copy `deploy/compose/.env.example` to `.env` first with real values.

## 7. Where this is most likely to break first
Roughly in order of likelihood (updated after the .NET 8 → .NET 10 migration, ADR-020, and the Postgres/Storage → Supabase move, ADR-024):
1. **Supabase connection pooling mode.** If you used the "Transaction pooler" connection string instead of "Session pooler"/"Direct connection" in Step 2, the app will very likely start fine and only misbehave subtly around Marten's async daemon (projection/subscription processing) - advisory locks don't work reliably under transaction-mode pooling. This is the specific kind of bug that's easy to burn hours on before suspecting the connection string, since nothing fails loudly at startup.
2. **`AspNetCore.HealthChecks.NpgSql`/`.Redis`/`.Rabbitmq`** — these are pinned to `9.0.0` as a good-faith guess. This is a community-maintained package (Xabaril) with its own versioning cadence, not tied to Microsoft's release train the way the auth packages are, so it's the least-verified pin in the whole file right now. If restore fails on these specifically, check https://www.nuget.org/packages/AspNetCore.HealthChecks.NpgSql for the actual current version.
3. **Wolverine's Marten event-forwarding API** (`m.SubscribeToEvent<DogLiked>()` in `Api.Host/Program.cs`) — flagged from the start as needing verification, and now doubly so since Wolverine jumped from the 5.x line I originally assumed to 6.x. If Api.Host fails to start with a Wolverine configuration exception, this is the first place to check against the current WolverineFx.Marten docs for whatever 6.17.2 actually looks like.
4. **`Serilog.AspNetCore`** — left at `8.0.3` since Serilog versions independently of the .NET runtime and 8.0.3 likely still works fine on net10.0, but not independently re-verified against net10.0 compatibility.
5. **`AggregateStreamAsync<T>`** signature in `DetectMutualMatchHandler` — still fairly confident in this one (long-standing, stable Marten API), but Marten jumped two major versions (7→9) from my original assumption, so some API surface may have shifted even here.
6. **Supabase JWT validation** — double-check the actual `iss`/`aud` claims in a real Supabase-issued token match what's hardcoded in `Program.cs` if you get "IDX10205: Issuer validation failed" or an audience error - these were configured from Supabase's documented conventions, not confirmed against a live token.

If you hit an error, paste it back with which step you were on. Given how fast Marten/Wolverine are moving right now, a quick way to self-serve on any remaining version mismatch: the NuGet.org package page for whatever's failing (`https://www.nuget.org/packages/<PackageName>`) shows the current version and its supported target frameworks directly — often faster than waiting on a round trip here.
