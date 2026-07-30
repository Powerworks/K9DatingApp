---
name: build-state-view
description: Implements a Wolverine.Http + Marten state-view slice (query endpoint, and a projector if the read model isn't a raw document) from a slice.json definition, on top of an event-sourced write side
---

# Build State View Slice

> Before doing anything else, read the slice definition from `build-kit-dotnet-es/.slices/{Context}/{slicename}/slice.json`. This file is the **source of truth** for all fields, events, and read-model shape. Never invent fields not defined there. Run `load-slice` first if this file might be stale.

**Write the projector's test before the projector**, and the query handler's test before/alongside it — see Step 4.

---

## What a State View Slice is

A state-view slice is a read model: `EVENT(s) → READMODEL → SCREEN/CALLER`. It never emits events or processes commands. It has up to two halves:

1. **The query** — a `[WolverineGet]` endpoint that reads a Marten document and returns a response.
2. **The projector** — only needed if the read model isn't just "this entity's own Inline snapshot, read back as-is." Keeps a dedicated read-model document current by reacting to the event(s) that should update it.

**Important — this does not conflict with "everything is event-sourced" (see `build-state-change`'s Step 2)**: the *write side* (every domain entity) is always an event stream. The *read side* is a different concern — a read-model document is always a plain Marten document (`options.Schema.For<T>()`), whether it's the entity's own registered Inline snapshot being read directly, or a dedicated document a projector builds by reshaping/aggregating one or more event streams. "Event-sourced only" means "the source of truth is always events," not "every queryable document must itself be an event stream" — nothing in this kit event-sources a read model.

If the read model is nothing more than reading back an entity's own Inline snapshot by id, you only need the query half. If the read model aggregates/reshapes data from event(s) — across streams, or from another module's integration event — you need both halves.

---

## Step 1 — Read the slice.json

Extract:
- **sliceName** — the projection/query name
- **context** — bounded context → module
- **events[]** — events this projection reacts to (empty/absent if it's a direct read of an entity's own Inline snapshot, with no dedicated projector)
- **readModel / fields** — the shape of what the query returns

> **Comments & description**: same as `build-state-change` Step 1 — use `comments[]`/`description` as implementation hints, resolve used comments when done via the same `POST .../comments/<commentId>/resolve` call.

---

## Step 2 — Does this need a projector?

- **No projector needed** — the query reads an entity's own Inline snapshot directly via `LoadAsync<<Entity>>`/`Query<<Entity>>()`. This only works if that entity is already registered with `options.Projections.Snapshot<<Entity>>(SnapshotLifecycle.Inline)` in `<Context>Module.cs` (see `build-state-change`'s Step 5) — if it isn't yet, either add that registration (if this really is just "give me entity X back as-is") or build a projector (if the read model reshapes/aggregates, which is a signal it should stay separate from the entity's own snapshot rather than pulling the entity into a shape it was never meant to have). Skip to Step 5.
- **Projector needed** — the slice.json's `events[]` names event(s) this read model must react to that aren't just "an entity's own snapshot as-is" (a reshaped/aggregated view, a view spanning multiple streams, or a view fed by an event from a *different* module). Go to Step 3, then Step 5.

---

## Step 3 — The projector (if needed)

### The read-model document

`src/Modules/<Context>/<SolutionName>.Modules.<Context>.Domain/<ReadModelName>.cs` — plain public-settable class (**not** an `Entity` subclass with restricted access — read-model documents don't need `[JsonInclude]`/`[JsonConstructor]` since nothing restricts their setters, unlike the event-sourced entities in `build-state-change`):

```csharp
public class <ReadModelName>
{
    public Guid Id { get; set; }
    // ...one property per read-model field from slice.json
}
```

### The trigger

Two possible triggers — check which one applies from where the triggering event comes from:

**Same-module domain event** — the event is defined in this module's own `Domain/Events/<Context>Events.cs`, appended by a state-change or automation slice you've already built (or are building alongside this one). Confirm/add it to `AddMarten().IntegrateWithWolverine(m => m.SubscribeToEvent<T>())` in `Api.Host/Program.cs` — see Step 6.

**Cross-module integration event** (message-bus transport, e.g. RabbitMQ) — the event is another module's published integration event (its `<OtherModule>.Contracts` project). If this module doesn't yet consume any integration event, its `<Context>Module.cs` needs `IntegrationEventQueueName` set (Step 6) — without a bound queue, the published event has nowhere to land and is silently dropped, with no error anywhere: the publish succeeds, the exchange fans it out, and a queue-less consumer simply never receives it. This is a real, easy-to-miss gap the first time a module starts consuming across module boundaries — check for it explicitly rather than assuming a prior slice already wired it.

### `<TriggerEvent>Projector.cs`

File named after the trigger event; **class name must end in `Handler`** even though the file isn't — this is Wolverine's actual runtime discovery requirement, not just a style rule. A class named `<TriggerEvent>Projector` with a correct `Handle` method is silently never invoked — the message gets marked "handled" (meaning "no matching handler found, discarded"), not "processed," with zero rows ever written and no exception to point at the cause.

```csharp
using Marten;
using <SolutionName>.Modules.<Context>.Domain;
using <SolutionName>.Modules.<OtherContext>.Contracts; // only if cross-module

namespace <SolutionName>.Modules.<Context>.Api.ReadModels.<ReadModelName>;

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

**Always call `SaveChangesAsync` explicitly.** `IDocumentSession.Store(...)` only stages a change in-session; it does not auto-flush just because a handler takes `IDocumentSession` as a parameter. A projector that forgets this runs "successfully" — no exception, envelope marked handled — and silently writes nothing. This is the single easiest mistake to make in a projector, precisely because everything *looks* correct without it.

Delivery is at-least-once; Wolverine's inbox deduplicates by envelope id, and `Store()` is an upsert keyed by `Id`, so redelivery is safe without extra idempotency logic.

For an update/delete rather than a create, `LoadAsync`/`Query` the existing document first, or `session.Delete<T>(id)` — mirror whichever the slice.json's event semantics call for.

---

## Step 4 — Tests first (projector, if built)

Projectors touch real persistence, so they get a Testcontainers spec, written before/alongside the projector.

File: `tests/<SolutionName>.IntegrationTests/<Context>/<ReadModelName>ProjectorTests.cs`, using this module's Postgres fixture (create `<Context>PostgresFixture.cs` if one doesn't exist yet: `Testcontainers.PostgreSql`, one container per collection, `DocumentStore.For(opts => { module.MartenConfiguration.Configure(opts); opts.AutoCreateSchemaObjects = AutoCreate.All; })`).

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

File: `src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/ReadModels/<QueryName>/`

### `<QueryName>.cs` — response record(s)

```csharp
namespace <SolutionName>.Modules.<Context>.Api.ReadModels.<QueryName>;

public sealed record <QueryName>Response(/* ...fields the caller gets back, from slice.json readModel */);
```

### `<QueryName>Handler.cs`

Direct single-document read (parameterized route, `Results<Ok<T>, NotFound>`):

```csharp
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using <SolutionName>.Modules.<Context>.Domain;
using Wolverine.Http;

namespace <SolutionName>.Modules.<Context>.Api.ReadModels.<QueryName>;

public static class <QueryName>Handler
{
    [WolverineGet("/api/v1/<context-kebab>/<resource>/{id:guid}")]
    public static async Task<Results<Ok<<QueryName>Response>, NotFound>> Handle(
        Guid id,
        IQuerySession session,
        CancellationToken cancellationToken)
    {
        // Same LoadAsync call whether `<ReadModelName>` here is a projector-
        // built document or an entity's own Inline snapshot — Marten stores
        // both as plain queryable documents under the hood.
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

**Class name must end in `Handler`** — same Wolverine discovery rule as the projector and every command handler. No manual route registration — `[WolverineGet]` is discovered automatically the same way `[WolverinePost]` is.

Write this handler's own test (direct call, in-memory list or the same Layer-3 fixture if it uses `session.Query<T>()`) the same way `build-state-change`'s Layer 2/3 guidance describes — a query handler is tested exactly like a command handler, just asserting on the returned data instead of on `SaveChangesAsync` calls.

---

## Step 6 — Register the read model's Marten schema and trigger

In `src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/<Context>Module.cs`'s `IMartenModuleConfiguration.Configure`:

**If a projector was built** (a dedicated read-model document, distinct from any entity's Inline snapshot):

```csharp
options.Schema.For<<ReadModelName>>()
    .DatabaseSchemaName(SchemaName)
    .Index(x => x.SomeFilterField); // index whatever the query filters/sorts on
```

**If no projector was built** (reading an entity's own Inline snapshot directly), there's nothing new to register here — confirm the `options.Projections.Snapshot<<Entity>>(SnapshotLifecycle.Inline)` line from `build-state-change`'s Step 5 is already present; add it if this is the first query to need it.

No Flyway/SQL migration file either way — Marten manages the DDL.

**If the trigger is a cross-module integration event** and this module doesn't already consume one, also set (or confirm already set):

```csharp
public string? IntegrationEventQueueName => "<context-lowercase>.integration-events";
```

and confirm `Api.Host/Program.cs` binds it (it should already loop over every module's `IntegrationEventQueueName` and bind to the shared exchange automatically — nothing to add there for an existing module, but double-check this line is actually present if you're touching a module that's never consumed a cross-module event before).

**If the trigger is a same-module domain event**, confirm `Api.Host/Program.cs`'s `AddMarten().IntegrateWithWolverine(m => m.SubscribeToEvent<T>())` call includes the trigger event type — add it if missing.

---

## Step 7 — Quality checks

```bash
dotnet build <path-to-your-.NET-solution>/<SolutionName>.sln
dotnet test <path-to-your-.NET-solution>/<SolutionName>.sln --filter "FullyQualifiedName~<QueryName>|FullyQualifiedName~<ReadModelName>"
```

---

## Files to create / modify

```
src/Modules/<Context>/<SolutionName>.Modules.<Context>.Domain/
└── <ReadModelName>.cs                          ← only if a dedicated read-model doc is needed

src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/ReadModels/<QueryName>/
├── <QueryName>.cs                               ← response record(s)
├── <QueryName>Handler.cs                        ← the query
└── <TriggerEvent>Projector.cs (class ...Handler) ← only if a projector is needed

src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/<Context>Module.cs  ← Marten schema (+ IntegrationEventQueueName if new)

tests/<SolutionName>.IntegrationTests/<Context>/
└── <ReadModelName>ProjectorTests.cs             ← only if a projector was built

tests/<SolutionName>.Modules.<Context>.Tests/Handlers/  or  IntegrationTests
└── <QueryName>HandlerTests.cs
```

---

## Checklist

- [ ] Every field in the read model definition in slice.json has a property on the C# read-model class and response record — no invented fields
- [ ] Every event type in `events[]` is handled by the projector (or, if no projector, the query reads an entity's own Inline snapshot that already reflects them)
- [ ] `<TriggerEvent>ProjectorHandler`/`<QueryName>Handler` class names end in `Handler`
- [ ] Projector calls `SaveChangesAsync` explicitly
- [ ] Marten schema registered (`options.Schema.For<T>()` for a dedicated read model, or `Projections.Snapshot<T>()` if reading an entity's snapshot directly) — no SQL migration file created
- [ ] `IntegrationEventQueueName` set on the consuming module if this is its first cross-module event
- [ ] One Layer-3 test per specification in slice.json that exercises the projector; a direct test for the query handler
- [ ] No extra columns/fields added beyond what slice.json defines
- [ ] `dotnet build` and the slice's own tests pass
