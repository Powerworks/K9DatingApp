# Getting Started — Running PawMatch Locally

This scaffold has never been restored, built, or run (it was generated without NuGet/network access). Treat this as a strong starting point, not verified-working code — Section 5 below lists specifically where it's most likely to break and why.

## Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — **not .NET 8.** The solution originally targeted .NET 8, but current Marten (9.x) and Wolverine (6.x) have dropped net8.0 support entirely; see ADR-020 in the Solution Architecture doc. `global.json` pins the SDK to `10.0.100`.
- Docker Desktop (or equivalent) for the local infra containers
- `dotnet --version` should print `10.0.1xx`. If you have both .NET 8 and .NET 10 SDKs installed side by side, that's fine — `global.json`'s `rollForward: latestFeature` will pick the right one for this repo specifically without affecting other projects on your machine.

## 1. Start local infrastructure

```bash
docker compose -f deploy/compose/docker-compose.yml up -d
```

This brings up Postgres (with PostGIS), RabbitMQ, Redis, Keycloak, MinIO, and the Grafana LGTM-in-one-container image. Give it a minute — check everything's healthy:

```bash
docker compose -f deploy/compose/docker-compose.yml ps
```

Useful UIs once it's up:
| Service | URL | Login |
|---|---|---|
| RabbitMQ management | http://localhost:15672 | guest / guest |
| MinIO console | http://localhost:9001 | pawmatch / pawmatch_dev_only |
| Keycloak admin | http://localhost:8080 | admin / admin_dev_only |
| Grafana | http://localhost:3000 | admin / admin |

## 2. Set up the Keycloak realm (manual, one-time)

There's no realm-import file yet (deliberately — see Section 6), so this is a few clicks in the admin console:

1. Go to http://localhost:8080, log in as `admin`/`admin_dev_only`.
2. Top-left realm dropdown → **Create realm** → name it `pawmatch`.
3. **Clients** → **Create client**:
   - Client ID: `api-host`, type: OpenID Connect, turn OFF "Client authentication" (public isn't right for a resource-server-only client either, but this gets you unblocked — tighten this before anything beyond local testing).
   - Actually for `api-host`, simplest for now: you mostly just need the realm's issuer/JWKS endpoint to exist so `AddJwtBearer` has something to validate against — the client entry itself matters more for `blazor-app`.
4. **Clients** → **Create client** again:
   - Client ID: `blazor-app`, type: OpenID Connect, turn ON "Client authentication" (confidential), turn ON "Standard flow".
   - Valid redirect URIs: `http://localhost:5200/*`
   - Save, then go to the **Credentials** tab and copy the client secret — you'll need it once Blazor.App actually has OIDC wired up (it doesn't yet — see Section 6).
5. **Users** → **Add user** → create a test owner account, then set a password under the **Credentials** tab (turn off "Temporary").

## 3. Create the MinIO bucket

Media module isn't scaffolded yet, so nothing needs this today, but it's a 30-second step to have ready:
1. http://localhost:9001 → log in → **Buckets** → **Create Bucket** → `pawmatch-media`.

## 4. Restore and build

```bash
cd PawMatch
dotnet restore
dotnet build
```

`Directory.Packages.props` uses floating versions (`7.*`, `2.*`, etc.) since I couldn't reach NuGet to pin exact versions when generating this. **Expect `dotnet restore` to pull whatever the current latest patch is** — if `dotnet build` then fails on an API mismatch, that's the most likely first cause: check what actually got restored (`dotnet list package`) against what the code assumes, and pin exact versions in `Directory.Packages.props` once you know what works.

## 5. Run it

Three separate terminals (or run configs in your IDE):

```bash
# Terminal 1
dotnet run --project src/Host/PawMatch.Api.Host
# → http://localhost:5100/swagger

# Terminal 2
dotnet run --project src/Web/PawMatch.Blazor.App
# → http://localhost:5200

# Terminal 3 (optional at this stage - you can hit Api.Host/Blazor.App directly first)
dotnet run --project src/Gateway/PawMatch.Gateway
```

**Start with just Api.Host.** Get that compiling and serving Swagger before layering on Blazor.App and Gateway — it's the piece with the most moving parts (Marten, Wolverine, Redis, Keycloak auth all wire up in one `Program.cs`).

Quick smoke test once Api.Host is running:
```bash
curl http://localhost:5100/healthz/live
curl -X POST http://localhost:5100/api/v1/profiles/dogs -H "Content-Type: application/json" -H "Authorization: Bearer <-H "Authorization: Bearer "" \
  -d '{"name":"Rex","breed":"Labrador","ageInMonths":24,"bio":"Good boy","latitude":53.35,"longitude":-6.26}'
```
That second call will 401 — `CreateDogProfileHandler` requires an authenticated `ClaimsPrincipal` and there's no login flow wired up yet (Section 6). Getting a 401 rather than a 500 is actually the useful signal here — it means the app started and auth middleware is running.

## 6. What's deliberately not done yet (don't be surprised)
- **No login flow in Blazor.App** — `Program.cs` doesn't have OIDC wired up yet, only the typed HttpClient to the API. You can't get a real JWT through the UI yet; testing authenticated endpoints for now means getting a token directly from Keycloak's token endpoint with `curl`/Postman using the `blazor-app` client credentials from Step 2.
- **No Keycloak realm-import file** — once Step 2's manual setup is confirmed working, exporting it to `deploy/keycloak/pawmatch-realm.json` and adding `--import-realm` to the compose file removes the manual clicking for the next person.
- **`PawMatch.ArchitectureTests` doesn't exist yet** — the fitness-test rules described in the Event Modeling blueprint (Section 6) are written down but not implemented as actual NetArchTest code.
- **No unit/integration tests at all yet** — `tests/` isn't scaffolded.
- **No Dockerfiles** — not needed for local `dotnet run` testing, only for actually deploying.

## 7. Where this is most likely to break first
Roughly in order of likelihood (updated after the .NET 8 → .NET 10 migration, ADR-020):
1. **`AspNetCore.HealthChecks.NpgSql`/`.Redis`/`.Rabbitmq`** — these are pinned to `9.0.0` as a good-faith guess. This is a community-maintained package (Xabaril) with its own versioning cadence, not tied to Microsoft's release train the way the auth packages are, so it's the least-verified pin in the whole file right now. If restore fails on these specifically, check https://www.nuget.org/packages/AspNetCore.HealthChecks.NpgSql for the actual current version.
2. **Wolverine's Marten event-forwarding API** (`m.SubscribeToEvent<DogLiked>()` in `Api.Host/Program.cs`) — flagged from the start as needing verification, and now doubly so since Wolverine jumped from the 5.x line I originally assumed to 6.x. If Api.Host fails to start with a Wolverine configuration exception, this is the first place to check against the current WolverineFx.Marten docs for whatever 6.17.2 actually looks like.
3. **`Serilog.AspNetCore`** — left at `8.0.3` since Serilog versions independently of the .NET runtime and 8.0.3 likely still works fine on net10.0, but not independently re-verified against net10.0 compatibility.
4. **`AggregateStreamAsync<T>`** signature in `DetectMutualMatchHandler` — still fairly confident in this one (long-standing, stable Marten API), but Marten jumped two major versions (7→9) from my original assumption, so some API surface may have shifted even here.
5. **Keycloak JWT validation** — double-check the issuer Keycloak puts in tokens matches `Keycloak:Authority` exactly if you get "IDX10205: Issuer validation failed."

If you hit an error, paste it back with which step you were on. Given how fast Marten/Wolverine are moving right now, a quick way to self-serve on any remaining version mismatch: the NuGet.org package page for whatever's failing (`https://www.nuget.org/packages/<PackageName>`) shows the current version and its supported target frameworks directly — often faster than waiting on a round trip here.
