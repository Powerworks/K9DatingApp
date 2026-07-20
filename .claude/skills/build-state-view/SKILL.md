---
name: build-state-view
description: Implements a Wolverine.Http + Marten state-view slice (query endpoint, and a projector if the read model isn't a raw document) from a slice.json definition
---

# Build State View Slice

> Before doing anything else, read the slice definition from `build-kit-dotnet/.slices/{Context}/{slicename}/slice.json`. This file is the **source of truth** for all fields, events, and read-model shape. Never invent fields not defined there. Run `load-slice` first if this file might be stale.

**Write the projector's test before the projector**, and the query handler's test before/alongside it — see Step 4.

---

## What a State View Slice is

A state-view slice is a read model: `EVENT(s) → READMODEL → SCREEN/CALLER`. It never emits events or processes commands. It has up to two halves:

1. **The query** — a `[WolverineGet]` endpoint that reads a Marten document and returns a response.
2. **The projector** — only needed if the read model isn't just "the same document a command slice already stores." Keeps a dedicated read-model document current by reacting to the event(s) that should update it.

If the read model is nothing more than an existing document from a state-change slice (e.g. reading back the same `Application` a command slice stores), you only need the query half — see `GetDogProfileHandler` for that shape. If the read model aggregates/reshapes data from event(s), possibly from another module, you need both halves — see `GetDiscoveryFeedHandler` + `DogProfileCreatedProjectorHandler`.

---

## Step 1 — Read the slice.json

Extract:
- **sliceName** — the projection/query name
- **context** — bounded context → module
- **events[]** — events this projection reacts to (empty/absent if it's a direct document read with no dedicated projector)
- **readModel / fields** — the shape of what the query returns

> **Comments & description**: same as `build-state-change` Step 1 — use `comments[]`/`description` as implementation hints, resolve used comments when done via the same `POST .../comments/<commentId>/resolve` call.

---

## Step 2 — Does this need a projector?

- **No projector needed** — the query reads an existing document (created/updated by a state-change slice already built, or being built in the same session) directly via `LoadAsync`/`Query<T>()`. Skip to Step 4.
- **Projector needed** — the slice.json's `events[]` names event(s) this read model must react to that aren't just "whatever a command slice already stores as-is" (a reshaped/aggregated view, or a view fed by an event from a *different* module). Go to Step 3, then Step 4.

---

## Step 3 — The projector (if needed)

### The read-model document

`src/Modules/<Context>/K9Crush.Modules.<Context>.Domain/<ReadModelName>.cs` — plain public-settable class (not an `Entity` subclass with restricted access — read-model documents don't need `[JsonInclude]`/`[JsonConstructor]` since nothing restricts their setters):

```csharp
public class <ReadModelName>
{
    public Guid Id { get; set; }
    // ...one property per read-model field from slice.json
}
```

### The trigger

Two possible triggers — check which one applies from where the triggering event comes from:

**Same-module domain event** (event-sourced module, Marten forwarding) — the event is defined in this module's own `Domain/Events/<Context>Events.cs`.

**Cross-module integration event** (RabbitMQ) — the event is another module's published `IIntegrationEvent` (its `<OtherModule>.Contracts` project, e.g. `K9Crush.Modules.Profiles.Contracts.DogProfileCreatedV1`). If this module doesn't yet consume any integration event, its `<Context>Module.cs` needs `IntegrationEventQueueName` set (Step 5) — without it, the published event has nothing bound to receive it and is silently dropped (a real, documented incident in this codebase).

### `<TriggerEvent>Projector.cs`

File named after the trigger event; **class name must end in `Handler`** even though the file isn't — this is Wolverine's actual runtime discovery requirement, not just a style rule. A class named `<TriggerEvent>Projector` with a correct `Handle` method is silently never invoked (this happened for real: the envelope showed status `Handled` — meaning "no handler found, discarded," not "processed" — with zero rows ever written).

```csharp
using Marten;
using K9Crush.Modules.<Context>.Domain;
using K9Crush.Modules.<OtherContext>.Contracts; // only if cross-module

namespace K9Crush.Modules.<Context>.Api.ReadModels.<ReadModelName>;

public static class <TriggerEvent>ProjectorHandler
{
    public static async Task Handle(<TriggerEvent> triggerEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        session.Store(new <ReadModelName>
        {
            Id = triggerEvent.SomeId,
            // ...map every read-model field from the trigger event's fields
        });

        await session.SaveChangesAsync(cancellationToken); // NOT optional — Store() only stages the change
    }
}
```

**Always call `SaveChangesAsync` explicitly.** `IDocumentSession.Store(...)` only stages a change in-session; it does not auto-flush just because a handler takes `IDocumentSession` as a parameter. A projector that forgets this runs "successfully" (no exception, envelope marked handled) and silently writes nothing — this is a real, previously-shipped bug in this exact slice.

Delivery is at-least-once; Wolverine's inbox deduplicates by envelope id, and `Store()` is an upsert keyed by `Id`, so redelivery is safe without extra idempotency logic.

For an update/delete rather than a create, `LoadAsync`/`Query` the existing document first, or `session.Delete<T>(id)` — mirror whichever the slice.json's event semantics call for.

---

## Step 4 — Tests first (projector, if built)

Per `TestingApproach/TestingApproach.md` Layer 3 — projectors touch real persistence, so they get a Testcontainers spec, written before/alongside the projector:

File: `tests/K9Crush.IntegrationTests/<Context>/<ReadModelName>ProjectorTests.cs`, using this module's Postgres fixture (create `<Context>PostgresFixture.cs` if one doesn't exist yet — see `ShelterAdoptionPostgresFixture.cs` for the reference pattern: `Testcontainers.PostgreSql`, one container per collection, `DocumentStore.For(opts => { module.MartenConfiguration.Configure(opts); opts.AutoCreateSchemaObjects = AutoCreate.All; })`).

```csharp
[Fact]
public async Task Handle_On<TriggerEvent>_StoresReadModelRow()
{
    await using var session = fixture.Store.LightweightSession();

    await <TriggerEvent>ProjectorHandler.Handle(
        new <TriggerEvent>(/* ...fields... */),
        session,
        CancellationToken.None);

    var stored = await session.LoadAsync<<ReadModelName>>(expectedId);
    stored.Should().NotBeNull();
    stored!.SomeField.Should().Be(expectedValue);
}
```

One test per specification in slice.json that exercises the projector.

---

## Step 5 — The query handler

File: `src/Modules/<Context>/K9Crush.Modules.<Context>.Api/ReadModels/<QueryName>/`

### `<QueryName>.cs` — response record(s)

```csharp
namespace K9Crush.Modules.<Context>.Api.ReadModels.<QueryName>;

public sealed record <QueryName>Response(/* ...fields the caller gets back, from slice.json readModel */);
```

### `<QueryName>Handler.cs`

Direct single-document read (parameterized route, `Results<Ok<T>, NotFound>`):

```csharp
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.<Context>.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.<Context>.Api.ReadModels.<QueryName>;

public static class <QueryName>Handler
{
    [WolverineGet("/api/v1/<context-kebab>/<resource>/{id:guid}")]
    public static async Task<Results<Ok<<QueryName>Response>, NotFound>> Handle(
        Guid id,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        var doc = await session.LoadAsync<<ReadModelName>>(id, cancellationToken);
        if (doc is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new <QueryName>Response(doc.Id /* , ...map fields */));
    }
}
```

Collection/filtered read (query params, no route id):

```csharp
[WolverineGet("/api/v1/<context-kebab>/<resource>")]
public static async Task<<QueryName>Response> Handle(
    /* filter params as method parameters, e.g. */ double latitude, double longitude,
    IQuerySession session,
    CancellationToken cancellationToken)
{
    var candidates = await session.Query<<ReadModelName>>().ToListAsync(cancellationToken);
    var items = candidates.Where(/* filter per slice.json */).ToList();
    return new <QueryName>Response(items);
}
```

`**Class name must end in `Handler`** — same Wolverine discovery rule as the projector and every command handler. No manual route registration — `[WolverineGet]` is discovered automatically the same way `[WolverinePost]` is.

Write this handler's own test (direct call, in-memory list or the same Layer-3 fixture if it uses `session.Query<T>()`) the same way `build-state-change`'s Layer 2/3 guidance describes — a query handler is tested exactly like a command handler, just asserting on the returned data instead of on `Store`/`SaveChangesAsync` calls.

---

## Step 6 — Register the read model's Marten schema

In `src/Modules/<Context>/K9Crush.Modules.<Context>.Api/<Context>Module.cs`'s `IMartenModuleConfiguration.Configure`:

```csharp
options.Schema.For<<ReadModelName>>()
    .DatabaseSchemaName(SchemaName)
    .Index(x => x.SomeFilterField); // index whatever the query filters/sorts on
```

No Flyway/SQL migration file — Marten manages the DDL.

**If the trigger is a cross-module integration event** and this module doesn't already consume one, also set (or confirm already set) in the same file:

```csharp
public string? IntegrationEventQueueName => "<context-lowercase>.integration-events";
```

and confirm `Api.Host/Program.cs` binds it (it already loops over every module's `IntegrationEventQueueName` and binds to the `k9crush.events` exchange automatically — nothing to add there for an existing module, but double-check this line is actually present if you're touching a module that's never consumed a cross-module event before).

**If the trigger is a same-module domain event**, confirm `Api.Host/Program.cs`'s `AddMarten().IntegrateWithWolverine(m => m.SubscribeToEvent<T>())` call includes the trigger event type — add it if missing.

---

## Step 7 — Quality checks

```bash
dotnet build code/K9Crush-scaffold/K9Crush/K9Crush.sln
dotnet test code/K9Crush-scaffold/K9Crush/K9Crush.sln --filter "FullyQualifiedName~<QueryName>|FullyQualifiedName~<ReadModelName>"
```

---

## Files to create / modify

```
src/Modules/<Context>/K9Crush.Modules.<Context>.Domain/
└── <ReadModelName>.cs                          ← only if a dedicated read-model doc is needed

src/Modules/<Context>/K9Crush.Modules.<Context>.Api/ReadModels/<QueryName>/
├── <QueryName>.cs                               ← response record(s)
├── <QueryName>Handler.cs                        ← the query
└── <TriggerEvent>Projector.cs (class ...Handler) ← only if a projector is needed

src/Modules/<Context>/K9Crush.Modules.<Context>.Api/<Context>Module.cs  ← Marten schema (+ IntegrationEventQueueName if new)

tests/K9Crush.IntegrationTests/<Context>/
└── <ReadModelName>ProjectorTests.cs             ← only if a projector was built

tests/K9Crush.Modules.<Context>.Tests/Handlers/  or  IntegrationTests
└── <QueryName>HandlerTests.cs
```

---

## Checklist

- [ ] Every field in the read model definition in slice.json has a property on the C# read-model class and response record — no invented fields
- [ ] Every event type in `events[]` is handled by the projector (or, if no projector, the query reads an existing document that already reflects them)
- [ ] `<TriggerEvent>ProjectorHandler`/`<QueryName>Handler` class names end in `Handler`
- [ ] Projector calls `SaveChangesAsync` explicitly
- [ ] Marten schema registered (`options.Schema.For<T>()`) — no SQL migration file created
- [ ] `IntegrationEventQueueName` set on the consuming module if this is its first cross-module event
- [ ] One Layer-3 test per specification in slice.json that exercises the projector; a direct test for the query handler
- [ ] No extra columns/fields added beyond what slice.json defines
- [ ] `dotnet build` and the slice's own tests pass
