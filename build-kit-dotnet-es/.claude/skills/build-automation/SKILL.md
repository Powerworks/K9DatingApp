---
name: build-automation
description: Implements a Wolverine + Marten automation slice (a handler triggered by an event, which decides and acts) from a slice.json definition, on top of an event-sourced write side
---

# Build Automation Slice

> Before doing anything else, read the slice definition from `build-kit-dotnet-es/.slices/{Context}/{slicename}/slice.json`. This file is the **source of truth** for which trigger event drives the automation and what it does in response. Run `load-slice` first if this file might be stale.

**Write the test before the handler** — see Step 5.

---

## What an Automation Slice is

`EVENT(s) → AUTOMATION → COMMAND/EVENT(s)`. An automation is a Wolverine handler triggered by an event, never by an HTTP request — it reacts, decides, and acts (appends further event(s) or cascades an integration event for other modules to consume). It has **no route, no `[WolverinePost]`/`[WolverineGet]`**.

This is exactly the lane a state-change slice must *not* drift into. If you're building this skill because a command slice's slice.json describes a further consequence beyond its own direct result (e.g. "...and if this creates a mutual match, notify both owners" tucked into a command's `description`), that consequence belongs here, triggered by the event the command slice already emits — not inlined into the command handler. Bundling a downstream consequence into a command handler, then having to split it apart later once the mistake surfaces, is a well-worn failure mode worth avoiding from the start rather than discovering firsthand.

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

**Same-module domain event** — the trigger is one of this module's own domain event types, delivered via Marten forwarding: `AddMarten().IntegrateWithWolverine(m => m.SubscribeToEvent<TEvent>())` in `Api.Host/Program.cs`. Confirm the trigger type is registered there; add it if this is the first automation reacting to it.

**Cross-module integration event** (message-bus transport) — the trigger is another module's published integration event from its `<OtherModule>.Contracts` project. The *consuming* module needs its own durable queue: `<Context>Module.cs`'s `IntegrationEventQueueName => "<context-lowercase>.integration-events"`. Without this, the published event has nowhere to land and is silently dropped — no exception anywhere, just a downstream read model or automation that never fires. If this module already consumes at least one integration event, it already has this — check `<Context>Module.cs` before assuming you need to add it.

---

## Step 3 — Compute decision state (only if the decision needs stream history)

If the automation's decision requires more than just the trigger event's own fields (e.g. "has the *other* side already acted too"), it needs state — computed **live**, every invocation, never from a persisted or shared snapshot:

```csharp
using <SolutionName>.Modules.<Context>.Domain.Events;

namespace <SolutionName>.Modules.<Context>.Api.Automations.<AutomationName>;

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

Loaded per invocation via `session.Events.AggregateStreamAsync<T>(streamId, token: cancellationToken)` — Marten replays the stream through the `Apply(...)` methods every call.

**Never register this as `Projections.Snapshot<T>()`, never persist it, and never reference it from a second command/automation** — a second handler needing "similar-looking" state gets its own `[OtherName]State` type, even if the two look nearly identical today. This is a stricter rule than it might look at first: it's tempting to reuse or persist a decision-state type once two handlers need "basically the same thing," but that coupling is exactly what makes the *next* change to either handler risky — a field added for automation A's decision now silently affects automation B's too, and a persisted/shared version of that state can drift from what replaying the stream would actually produce. Keep it single-purpose, computed fresh, disposable.

**This is a different rule from `build-state-change`'s Inline snapshot guidance — don't conflate the two.** An entity's own Inline snapshot (registered in `<Context>Module.cs`, read back by `LoadAsync`/queries) is the entity's durable, shared, cross-slice identity — multiple read models and handlers are *meant* to depend on it, and persisting it is the whole point. A `[AutomationName]State` here is the opposite: a single handler's private, throwaway lens on a stream, computed fresh every time, that nothing else should ever reach for. If you find yourself wanting to reuse a `[AutomationName]State` from a second handler, that's a signal either the second handler needs its own state type, or what you actually want is a proper Inline-snapshotted entity — not a shortcut through someone else's automation state.

---

## Step 4 — The handler

File: `src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/Automations/<AutomationName>/<AutomationName>Handler.cs`

**Class name must end in `Handler`** — same Wolverine discovery requirement as command handlers and projectors; a correctly-written `Handle` method in a differently-named class is silently never invoked.

### Same-module trigger, cascading a new event + integration event

```csharp
using Marten;
using <SolutionName>.Modules.<Context>.Contracts;
using <SolutionName>.Modules.<Context>.Domain;
using <SolutionName>.Modules.<Context>.Domain.Events;

namespace <SolutionName>.Modules.<Context>.Api.Automations.<AutomationName>;

public static class <AutomationName>Handler
{
    public static async Task<<IntegrationEvent>?> Handle(
        <TriggerEvent> domainEvent,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var streamId = domainEvent.SomeId; // or a deterministic composite key — see note below

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

Returning an integration event from `Handle` is Wolverine's cascading-message convention — it publishes through the same durable outbox as everything else, so other modules never see a consequence that didn't actually commit. Return `null`/nothing when there's no consequence this invocation (an idempotency guard against acting twice on redelivered or repeated events — the `shouldAct` check above is exactly that guard).

If the stream identity is derived from more than one id (e.g. an unordered pair of participants), add a small deterministic helper (e.g. `<SomeStream>.IdFor(idA, idB)`, sorting the pair before hashing/combining) rather than inlining that logic in the handler — it needs to produce the same stream id regardless of argument order, and that's easy to get subtly wrong inline.

### Cross-module trigger, mutating an entity

```csharp
using Marten;
using <SolutionName>.Modules.<Context>.Domain;
using <SolutionName>.Modules.<OtherContext>.Contracts;

namespace <SolutionName>.Modules.<Context>.Api.Automations.<AutomationName>;

public static class <AutomationName>Handler
{
    public static async Task Handle(<TriggerIntegrationEvent> integrationEvent, IDocumentSession session, CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWriting<<Entity>>(integrationEvent.SomeId, cancellationToken);
        if (stream.Aggregate is null || /* already-done check, e.g. */ stream.Aggregate.SomeFlag)
            return; // idempotent no-op on redelivery or an already-applied change

        var @event = stream.Aggregate.SomeDomainMethod();
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);
    }
}
```

Always guard for idempotency (delivery is at-least-once) — check the target's current state before acting, same as the "already-done check" above.

If a new domain/integration event type is needed, add it per `build-state-change`'s guidance (domain events in `Domain/Events/<Context>Events.cs`) or as a new `sealed record ... : IIntegrationEvent` in this module's `.Contracts` project — always `EventId`, `OccurredAt`, plus whatever the consuming module needs, versioned by name suffix like `V1` so a future breaking change adds `V2` rather than editing this one.

---

## Step 5 — Test first

An automation handler is tested the same way a command handler is (Layer 2 where the stream-fetch/aggregate calls are mockable enough to be worth it; Layer 3 — Testcontainers — otherwise, which in practice is most of the time for an event-sourced handler).

File: `tests/<SolutionName>.Modules.<Context>.Tests/Handlers/<AutomationName>HandlerTests.cs` (Layer 2) or `tests/<SolutionName>.IntegrationTests/<Context>/<AutomationName>IntegrationTests.cs` (Layer 3).

Cover, at minimum, one test per specification in slice.json plus:
- The "should act" case (state satisfies the condition → event appended, integration event returned/none)
- The "should not act yet" case (condition not yet met → no-op, no exception)
- The idempotency case (already acted / redelivered event → no duplicate effect)

Event-sourced example shape (`AggregateStreamAsync` needs a real event store, so this is Layer 3):

```csharp
[Fact]
public async Task Handle_When<Condition>_Appends<ProducedEvent>AndReturnsIntegrationEvent()
{
    await using var session = fixture.Store.LightweightSession();
    var streamId = /* ...derive the same stream id the handler will use... */;
    session.Events.Append(streamId, new <TriggerEvent>(/* ... */, DateTimeOffset.UtcNow));
    await session.SaveChangesAsync();

    await using var handlerSession = fixture.Store.LightweightSession();
    var result = await <AutomationName>Handler.Handle(
        new <TriggerEvent>(/* the second, condition-completing event's fields */, DateTimeOffset.UtcNow), handlerSession, CancellationToken.None);

    result.Should().NotBeNull();
}
```

---

## Step 6 — Wire up the trigger registration

**Same-module domain event**: confirm/add the event type to `AddMarten().IntegrateWithWolverine(m => m.SubscribeToEvent<TEvent>())` in `Api.Host/Program.cs`.

**Cross-module integration event**: confirm/add `IntegrationEventQueueName` on the consuming module's `<Context>Module.cs` (Step 2). `Api.Host/Program.cs` should already loop over every module binding its declared queue name to the shared exchange — nothing else to change there for an existing module.

No separate schema-migration call to add either way — Marten's own schema auto-creation (`AutoCreateSchemaObjects`, see `Program.cs`) covers it.

---

## Step 7 — Quality checks

```bash
dotnet build <path-to-your-.NET-solution>/<SolutionName>.sln
dotnet test <path-to-your-.NET-solution>/<SolutionName>.sln --filter "FullyQualifiedName~<AutomationName>"
```

---

## Files to create / modify

```
src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/Automations/<AutomationName>/
├── <AutomationName>State.cs    ← only if the decision needs stream history
└── <AutomationName>Handler.cs

src/Modules/<Context>/<SolutionName>.Modules.<Context>.Contracts/  ← only if a new integration event is needed
└── <NewIntegrationEvent>V1.cs

src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/<Context>Module.cs  ← IntegrationEventQueueName, if new

src/Host/<SolutionName>.Api.Host/Program.cs  ← SubscribeToEvent<T>() registration, if new same-module trigger

tests/<SolutionName>.Modules.<Context>.Tests/Handlers/  or  tests/<SolutionName>.IntegrationTests/<Context>/
└── <AutomationName>{HandlerTests,IntegrationTests}.cs
```

---

## Checklist

- [ ] `<AutomationName>Handler` class name ends in `Handler`
- [ ] No route/`[WolverineGet]`/`[WolverinePost]` on this handler — automations are never called directly by a client
- [ ] Trigger event registered (Marten `SubscribeToEvent<T>` or the module's `IntegrationEventQueueName`) — grep to confirm it isn't already there before adding a duplicate
- [ ] `<AutomationName>State`, if used, is computed via `AggregateStreamAsync`, never persisted, never referenced by any other handler
- [ ] Idempotency: redelivering the trigger event does not double-act (checked explicitly in a test)
- [ ] Command/event data fields map exclusively from fields available on the trigger event or an explicitly loaded entity per slice.json — no invented mappings
- [ ] No filtering/decision conditions were invented — all conditions come from slice.json `description` or `comments`
- [ ] No field names were assumed or guessed — if a field is not in slice.json, it is not in the code
- [ ] `dotnet build` and the slice's own tests pass
