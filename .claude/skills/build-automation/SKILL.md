---
name: build-automation
description: Implements a Wolverine + Marten automation slice (a handler triggered by an event, which decides and acts) from a slice.json definition
---

# Build Automation Slice

> Before doing anything else, read the slice definition from `build-kit-dotnet/.slices/{Context}/{slicename}/slice.json`. This file is the **source of truth** for which trigger event drives the automation and what it does in response. Run `load-slice` first if this file might be stale.

**Write the test before the handler** — see Step 5.

---

## What an Automation Slice is

`EVENT(s) → AUTOMATION → COMMAND/EVENT(s)`. An automation is a Wolverine handler triggered by an event, never by an HTTP request — it reacts, decides, and acts (appends further event(s), mutates a document, or cascades an integration event for other modules to consume). It has **no route, no `[WolverinePost]`/`[WolverineGet]`**.

Per `docs/05-event-modeling-blueprint.md` Section 2: this is exactly the lane a state-change slice must *not* drift into. If you're building this skill because a command slice's slice.json describes a further consequence beyond its own direct result (e.g. "...and if this creates a mutual match, notify both owners" tucked into a command's `description`), that consequence belongs here, triggered by the event the command slice already emits/stores — not inlined into the command handler. This is not a hypothetical: the original `SwipeOnDog` handler did exactly this and had to be split into `SwipeOnDogHandler` (append-only) + `DetectMutualMatchHandler` (the automation, triggered by the event `SwipeOnDog` appends).

---

## Step 1 — Read the slice.json

Extract:
- **sliceName** — what this automation does (becomes the handler name)
- **context** — bounded context → module
- **processors[]** — each defines `triggerEvent`, and what the automation should produce
- **events[]** — event(s) this automation may append/cascade

> **Comments & description**: same as the other two skills — use `comments[]`/`description` as implementation hints, resolve used comments when done via `POST .../comments/<commentId>/resolve`.

If `sliceType === "TRANSLATION"` in the slice.json (a slice with no clear command/event/read-model shape of its own — just a description/notes), default to this skill unless the `description`/`notes` clearly indicate otherwise.

---

## Step 2 — Identify the trigger and its delivery mechanism

**Same-module domain event** (event-sourced module) — the trigger is one of this module's own `IDomainEvent` types, delivered via Marten forwarding: `AddMarten().IntegrateWithWolverine(m => m.SubscribeToEvent<TEvent>())` in `Api.Host/Program.cs`. Confirm the trigger type is registered there; add it if this is the first automation reacting to it.

**Cross-module integration event** (RabbitMQ) — the trigger is another module's published `IIntegrationEvent` from its `<OtherModule>.Contracts` project. The *consuming* module needs its own durable queue: `<Context>Module.cs`'s `IntegrationEventQueueName => "<context-lowercase>.integration-events"`. Without this, the published event is published-only and silently dropped — a real, previously-confirmed incident in this codebase (`RabbitMQ` queue list showed nothing bound to the exchange; a downstream read model stayed empty with no error anywhere). If this module already consumes at least one integration event, it already has this — check `<Context>Module.cs` before assuming you need to add it.

---

## Step 3 — Compute state (only if the decision needs history)

If the automation's decision requires more than just the trigger event's own fields (e.g. "has the *other* side already liked back"), it needs state — computed **live**, per ADR-019, never from a persisted/shared snapshot.

### Event-sourced module: `[AutomationName]State`

File: `src/Modules/<Context>/K9Crush.Modules.<Context>.Api/Automations/<AutomationName>/<AutomationName>State.cs`

```csharp
using K9Crush.Modules.<Context>.Domain.Events;

namespace K9Crush.Modules.<Context>.Api.Automations.<AutomationName>;

public sealed class <AutomationName>State
{
    public Guid SomeId { get; private set; }
    public bool ConditionA { get; private set; }
    public bool ConditionB { get; private set; }

    public void Apply(<SomeEvent> e)
    {
        SomeId = e.SomeId;
        ConditionA = true;
    }

    public void Apply(<OtherEvent> e)
    {
        ConditionB = true;
    }
}
```

Loaded per invocation via `session.Events.AggregateStreamAsync<T>(streamId, token: cancellationToken)` — Marten replays the stream through the `Apply(...)` methods every call. **Never** register this as `Projections.Snapshot<T>()`, never persist it, and never reference it from a second command/automation — a second handler needing "similar-looking" state gets its own `[OtherName]State` type. This exact bundling mistake (a shared, persisted `MatchAggregate`) was made once in this codebase and had to be reverted; the fitness-test suite (`K9Crush.ArchitectureTests`) is meant to catch it mechanically going forward.

### Document-store module: no separate state type needed

Just `LoadAsync<T>` the document(s) the decision depends on directly in the handler (see `PromoteOwnerToShelterOnAccountCreatedHandler` — it loads `OwnerAccount` and checks `Role` inline, no separate state type, since document-store modules already have durable current-state documents rather than an event stream to replay).

---

## Step 4 — The handler

File: `src/Modules/<Context>/K9Crush.Modules.<Context>.Api/Automations/<AutomationName>/<AutomationName>Handler.cs`

**Class name must end in `Handler`** — same Wolverine discovery requirement as command handlers and projectors; a correctly-written `Handle` method in a differently-named class is silently never invoked.

### Event-sourced, same-module trigger, cascading a new event + integration event

```csharp
using Marten;
using K9Crush.Modules.<Context>.Contracts;
using K9Crush.Modules.<Context>.Domain;
using K9Crush.Modules.<Context>.Domain.Events;

namespace K9Crush.Modules.<Context>.Api.Automations.<AutomationName>;

public static class <AutomationName>Handler
{
    public static async Task<<IntegrationEvent>?> Handle(
        <TriggerEvent> domainEvent,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var streamId = <StreamKeyHelper>.IdFor(domainEvent.SomeId /* , ... */);

        var state = await session.Events.AggregateStreamAsync<<AutomationName>State>(
            streamId, token: cancellationToken);

        var shouldAct = state is { ConditionA: true, ConditionB: true } /* && !state.AlreadyDone */;
        if (!shouldAct)
            return null; // nothing to do — no cascaded message published

        var now = DateTimeOffset.UtcNow;
        session.Events.Append(streamId, new <ProducedEvent>(/* ... */, now));
        await session.SaveChangesAsync(cancellationToken);

        return new <IntegrationEvent>(
            EventId: Guid.NewGuid(),
            OccurredAt: now,
            /* ...fields other modules need */);
    }
}
```

Returning an `IIntegrationEvent` from `Handle` is Wolverine's cascading-message convention — it publishes through the same durable outbox as everything else, so other modules never see a consequence that didn't actually commit. Return `null`/nothing when there's no consequence this invocation (an idempotency guard against acting twice on redelivered or repeated events — see `DetectMutualMatchHandler`'s `isNewMutualMatch` check for the exact shape).

### Document-store, cross-module trigger, mutating a document

```csharp
using Marten;
using K9Crush.Modules.<Context>.Domain;
using K9Crush.Modules.<OtherContext>.Contracts;

namespace K9Crush.Modules.<Context>.Api.Automations.<AutomationName>;

public static class <AutomationName>Handler
{
    public static async Task Handle(<TriggerIntegrationEvent> integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        var entity = await session.LoadAsync<<Entity>>(integrationEvent.SomeId, cancellationToken);
        if (entity is null || /* already-done check, e.g. */ entity.SomeFlag)
            return; // idempotent no-op on redelivery or an already-applied change

        entity.SomeDomainMethod();
        session.Store(entity);
        await session.SaveChangesAsync(cancellationToken);
    }
}
```

Always guard for idempotency (delivery is at-least-once) — check the target's current state before acting, same as `PromoteOwnerToShelterOnAccountCreatedHandler`'s `ownerAccount.Role == OwnerRole.Shelter` early return.

If a new domain/integration event type is needed, add it per `build-state-change`'s Step 3b guidance (domain events in `Domain/Events/<Context>Events.cs`) or as a new `sealed record ... : IIntegrationEvent` in this module's `.Contracts` project (see `ShelterAccountCreatedV1`/`MatchCreatedV1` for the shape — always `EventId`, `OccurredAt`, plus whatever the consuming module needs, versioned by name suffix like `V1` so a future breaking change adds `V2` rather than editing this one).

---

## Step 5 — Test first

Per `TestingApproach/TestingApproach.md`. An automation handler is tested the same way a command handler is (Layer 2 if it only does `LoadAsync`/`Store`/`SaveChangesAsync`/`Events.Append`/`AggregateStreamAsync`; Layer 3 if it uses `session.Query<T>()`).

File: `tests/K9Crush.Modules.<Context>.Tests/Handlers/<AutomationName>HandlerTests.cs` (Layer 2) or `tests/K9Crush.IntegrationTests/<Context>/<AutomationName>IntegrationTests.cs` (Layer 3).

Cover, at minimum, one test per specification in slice.json plus:
- The "should act" case (state/document satisfies the condition → event appended/document mutated, integration event returned/none)
- The "should not act yet" case (condition not yet met → no-op, no exception)
- The idempotency case (already acted / redelivered event → no duplicate effect)

Event-sourced example shape (mirrors `DraftsIntegrationTests.cs`'s direct-call style, adapted — `AggregateStreamAsync` needs a real event store, so this is Layer 3):

```csharp
[Fact]
public async Task Handle_When<Condition>_Appends<ProducedEvent>AndReturnsIntegrationEvent()
{
    await using var session = fixture.Store.LightweightSession();
    var streamId = <StreamKeyHelper>.IdFor(idA, idB);
    session.Events.Append(streamId, new <TriggerEvent>(idA, idB, DateTimeOffset.UtcNow));
    await session.SaveChangesAsync();

    await using var handlerSession = fixture.Store.LightweightSession();
    var result = await <AutomationName>Handler.Handle(
        new <TriggerEvent>(idB, idA, DateTimeOffset.UtcNow), handlerSession, CancellationToken.None);

    result.Should().NotBeNull();
}
```

Document-store example shape (Layer 2, mockable):

```csharp
[Fact]
public async Task Handle_WhenAlreadyPromoted_DoesNothing()
{
    var ownerAccount = OwnerAccount.Create(ownerId, "a@b.com", DateTimeOffset.UtcNow);
    ownerAccount.PromoteToShelter();
    var session = Substitute.For<IDocumentSession>();
    session.LoadAsync<OwnerAccount>(ownerId, Arg.Any<CancellationToken>()).Returns(ownerAccount);

    await <AutomationName>Handler.Handle(new <TriggerIntegrationEvent>(Guid.NewGuid(), DateTimeOffset.UtcNow, ownerId, shelterAccountId), session, CancellationToken.None);

    await session.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
}
```

---

## Step 6 — Wire up the trigger registration

**Same-module domain event**: confirm/add the event type to `AddMarten().IntegrateWithWolverine(m => m.SubscribeToEvent<TEvent>())` in `Api.Host/Program.cs`.

**Cross-module integration event**: confirm/add `IntegrationEventQueueName` on the consuming module's `<Context>Module.cs` (Step 2). `Api.Host/Program.cs` already loops over every module binding its declared queue name to the `k9crush.events` exchange — nothing else to change there for an existing module.

Also confirm `findEventstore`-equivalent bootstrap is in place — in this codebase that's simply Marten's own schema auto-creation (`AutoCreateSchemaObjects`, see `Program.cs`); there is no separate `schema.migrate()` call to add, unlike the node/emmett version of this skill.

---

## Step 7 — Quality checks

```bash
dotnet build code/K9Crush-scaffold/K9Crush/K9Crush.sln
dotnet test code/K9Crush-scaffold/K9Crush/K9Crush.sln --filter "FullyQualifiedName~<AutomationName>"
```

---

## Files to create / modify

```
src/Modules/<Context>/K9Crush.Modules.<Context>.Api/Automations/<AutomationName>/
├── <AutomationName>State.cs    ← only if the decision needs stream history (event-sourced)
└── <AutomationName>Handler.cs

src/Modules/<Context>/K9Crush.Modules.<Context>.Contracts/  ← only if a new integration event is needed
└── <NewIntegrationEvent>V1.cs

src/Modules/<Context>/K9Crush.Modules.<Context>.Api/<Context>Module.cs  ← IntegrationEventQueueName, if new

src/Host/K9Crush.Api.Host/Program.cs  ← SubscribeToEvent<T>() registration, if new same-module trigger

tests/K9Crush.Modules.<Context>.Tests/Handlers/  or  tests/K9Crush.IntegrationTests/<Context>/
└── <AutomationName>{HandlerTests,IntegrationTests}.cs
```

---

## Checklist

- [ ] `<AutomationName>Handler` class name ends in `Handler`
- [ ] No route/`[WolverineGet]`/`[WolverinePost]` on this handler — automations are never called directly by a client
- [ ] Trigger event registered (Marten `SubscribeToEvent<T>` or the module's `IntegrationEventQueueName`) — grep to confirm it isn't already there before adding a duplicate
- [ ] `[AutomationName]State`, if used, is computed via `AggregateStreamAsync`, never persisted, never referenced by any other handler
- [ ] Idempotency: redelivering the trigger event does not double-act (checked explicitly in a test)
- [ ] Command/event data fields map exclusively from fields available on the trigger event or an explicitly loaded document per slice.json — no invented mappings
- [ ] No filtering/decision conditions were invented — all conditions come from slice.json `description` or `comments`
- [ ] No field names were assumed or guessed — if a field is not in slice.json, it is not in the code
- [ ] `dotnet build` and the slice's own tests pass
