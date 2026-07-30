---
name: build-state-change
description: Implements a Wolverine.Http + Marten state-change slice (request/response, handler, tests) from a slice.json definition, using Marten's event-sourcing mode exclusively
---

# Build State Change Slice

> Before doing anything else, read the slice definition from `build-kit-dotnet-es/.slices/{Context}/{slicename}/slice.json`. This file is the **source of truth** for all fields, events, and metadata. Never invent fields not defined there. If you haven't already, run the `load-slice` skill first to make sure this file is fresh.

**Write the tests before (or alongside) the handler, not after.** Layer 1/2 tests below describe the behavior you're about to build; they should exist and fail (or not compile) before `<SliceName>Handler.cs` does.

---

## What a State Change Slice is

A state-change slice processes a command:
1. Loads whatever state it needs to validate the command by replaying (or reading the self-aggregated snapshot of) an event stream
2. Validates the command against that state
3. Appends the resulting event(s) and returns a response

A state-change slice may only decide "is this request valid" — never "what else should happen as a consequence beyond emitting my own event(s)." If the slice.json's `description`/`comments` describe a *further* consequence (e.g. "...and if this creates a mutual match, notify both owners"), that further consequence is a separate **automation** slice (see the `build-automation` skill), triggered by the event this slice produces — do not build it inline here. Bundling a downstream consequence into a command handler is a mistake that's easy to make once and expensive to unwind later — split it out from the start.

---

## Step 1 — Read the slice.json

From the slice definition, extract:
- **sliceName** — the slice title (becomes the Command/request name)
- **context** — the bounded context → maps to a module (an existing one, or a new one following this project's module layout)
- **commands[]** — list of commands with their data fields
- **events[]** — list of events emitted by each command
- **specifications[]** — test scenarios (given/when/then)

> **Comments & description**: each element (commands, events, readmodels, processors, screens, tables) carries a `comments: string[]` array (board comments on that node) and a `description` field; the slice itself also has `comments: string[]`. Use these as implementation hints — pass them as code doc-comments, or validation logic where they add value. When done, resolve each used comment: `POST <BASE_URL>/api/org/<ORG_ID>/boards/<BOARD_ID>/nodes/<nodeId>/comments/<commentId>/resolve` (get comment IDs first via GET on the same path without the last two segments — see `connect`/`load-slice` for `TOKEN`/`BASE_URL`/etc.).

---

## Step 2 — Every entity is event-sourced

Unlike a hybrid setup where some modules use Marten as a plain document store and others use event streams, **this kit has one storage strategy: event sourcing, everywhere, always.** There is no per-module or per-slice decision to make here — skip straight to Step 3.

This isn't an arbitrary simplification. A real retrofit of a hybrid document-store/event-sourced codebase surfaced two costs that a single, consistent strategy avoids going forward:

- **A decision that has to be made and re-verified on every slice.** "Is this module document-store or event-sourced?" sounds like a one-time call, but a module's storage strategy is invisible from its slice.json — an agent (or a person) building slice #12 in a module has to go check `<Context>Module.cs` for `Schema.For<T>()` calls before writing a single line, every time, because guessing wrong means every downstream assumption (how to load state, how to test it, how to register its schema) is wrong too.
- **Silent staleness.** A written-down "document-store by default, event-sourced only for X" rule doesn't update itself when the project's actual direction changes later — the rule and the code drift apart, and nothing points that out until someone reads both closely enough to notice they disagree.

Committing to one strategy up front removes both failure modes: there's nothing to check, and nothing to go stale.

---

## Step 3 — The event-sourced entity pattern

Files: `src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/Commands/<SliceName>/`

### `<SliceName>.cs` — request + response records

```csharp
using System.ComponentModel.DataAnnotations;

namespace <SolutionName>.Modules.<Context>.Api.Commands.<SliceName>;

public sealed record <SliceName>Request(
    [property: Required, MaxLength(50)] string SomeField
    // ...one property per command data field in slice.json, with
    // [Required]/[MaxLength]/[Range]/etc. matching the field's constraints
    );

public sealed record <SliceName>Response(Guid <Entity>Id /* , ...other fields the caller needs back */);
```

If a request field needs a rule plain attributes can't express (a `Guid` that must not be `Guid.Empty`, a cross-field rule, "can't target itself"), implement `IValidatableObject` instead/additionally:

```csharp
public sealed record <SliceName>Request(Guid TargetId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TargetId == Guid.Empty)
            yield return new ValidationResult("TargetId is required.", [nameof(TargetId)]);
    }
}
```

**Do not** write a separate `AbstractValidator<T>`/FluentValidation class — `[WolverinePost]`/`[WolverineGet]` endpoints bypass Wolverine's message-bus validation pipeline entirely (this is a genuine, verifiable behavior of Wolverine.Http, not a style preference: FluentValidation hooks only into `IMessageBus.InvokeAsync`/`SendAsync`, which HTTP endpoints never go through — an invalid body reaches the handler and 500s instead of 400ing if you rely on it). Validation lives on the request record itself; `Program.cs`'s `opts.UseDataAnnotationsValidationProblemDetailMiddleware()` wires it centrally — no per-slice registration needed.

### The entity — self-aggregating, `Create`/`Apply`

`src/Modules/<Context>/<SolutionName>.Modules.<Context>.Domain/<Entity>.cs`:

```csharp
using System.Text.Json.Serialization;
using <SolutionName>.BuildingBlocks.Domain;
using <SolutionName>.Modules.<Context>.Domain.Events;

namespace <SolutionName>.Modules.<Context>.Domain;

public class <Entity> : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    // ...every property this entity exposes, [JsonInclude]'d (see note below)

    [JsonConstructor]
    private <Entity>() { }

    public static <Entity> Create(<SomeEvent> e) => new()
    {
        Id = e.SomeId,
        OwnerId = e.OwnerId,
        // ...map every field the entity needs from its creating event
    };

    public void Apply(<OtherEvent> e)
    {
        // mutate state unconditionally — this method does not guard its
        // own preconditions (see "Entities don't guard preconditions" below)
    }

    // Factory + mutator methods return the event they produce, having
    // already applied it to `this` — callers never construct+append an
    // event without also folding it into the in-memory entity:
    public static (<Entity> Entity, <SomeEvent> Event) CreateNew(Guid ownerId /* , ... */)
    {
        var @event = new <SomeEvent>(Guid.NewGuid(), ownerId, DateTimeOffset.UtcNow);
        return (Create(@event), @event);
    }

    public <OtherEvent> SomeDomainMethod(/* args */)
    {
        var @event = new <OtherEvent>(/* ... */);
        Apply(@event);
        return @event;
    }
}
```

This is Marten's "self-aggregating" convention: the entity type itself *is* the write-side projection. Marten discovers `Create`/`Apply` by convention when you `FetchForWriting<T>`/`AggregateStreamAsync<T>` — there's no separate `IProjection` class to write for the write model.

**`[JsonInclude]`/`[JsonConstructor]` are not optional** if the entity restricts its own constructor/setters (the normal DDD instinct — only factory/mutator methods produce a valid instance). Without them, Marten's `System.Text.Json`-based serializer cannot deserialize the entity back out of its snapshot. This is a read-path-only failure: `session.Events.Append`/`SaveChangesAsync` (the write path) work fine either way, so a slice can look completely correct — build passes, the command succeeds — right up until the first real read (`LoadAsync`, a query, or `AggregateStreamAsync` for another handler) throws `NotSupportedException`. Add both on every new entity, every time; don't wait for the read path to catch it.

**Entities do not guard their own preconditions.** No `if (Status != X) throw` inside `Apply`/a domain method — that guard belongs in the handler (Step 4), not the entity. `Apply` methods set state unconditionally and trust the caller (the handler, which has already checked current state) to only call them when valid.

### `<SliceName>Handler.cs` — the handler

```csharp
using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using <SolutionName>.Modules.<Context>.Domain;
using Wolverine.Http;

namespace <SolutionName>.Modules.<Context>.Api.Commands.<SliceName>;

public static class <SliceName>Handler
{
    [WolverinePost("/api/v1/<context-kebab>/<slice-kebab-route>")]
    [Authorize(Policy = "VerifiedOwner")] // or a role-gated policy — see your project's auth setup
    public static async Task<Results<Ok<<SliceName>Response>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        <SliceName>Request request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // FetchForWriting loads the entity by replaying its stream (or its
        // Inline snapshot, if registered — Step 5) AND hands back a session
        // primed for optimistic-concurrency append. Prefer this over a bare
        // AggregateStreamAsync/LoadAsync + separate Events.Append whenever
        // the handler is about to append based on what it just read.
        var stream = await session.Events.FetchForWriting<<Entity>>(request.SomeId, cancellationToken);
        if (stream.Aggregate is null)
            return TypedResults.NotFound();

        var entity = stream.Aggregate;
        if (entity.OwnerId != callerOwnerId) // only if the slice needs an ownership check
            return TypedResults.Forbid();

        // business rule checks from slice.json specifications[] — return
        // Conflict<string> for anything the slice.json calls out as a
        // named error scenario (SPEC_ERROR), NotFound/Forbid otherwise

        var @event = entity.SomeDomainMethod(/* ... */);
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new <SliceName>Response(entity.Id));
    }
}
```

For a brand-new entity (no prior stream), start it instead of fetching:

```csharp
var (entity, @event) = <Entity>.CreateNew(callerOwnerId /* , ... */);
session.Events.StartStream<<Entity>>(entity.Id, @event);
await session.SaveChangesAsync(cancellationToken);
```

**The class must be named `<SliceName>Handler`** — Wolverine's convention-based discovery only recognizes a `Handle` method if the containing class name ends in `Handler`. This is not optional; a correctly-implemented `Handle` method in a class named anything else is silently never registered. This is one of the highest-value things to double-check on every new handler, projector, or automation — it fails silently (no exception, no log line calling it out), and the only symptom is "nothing happened."

**Concurrency**: `FetchForWriting`/`FetchForExclusiveWriting` are optimistic by default — `SaveChangesAsync` throws if the stream moved since it was fetched. The real exception type is `JasperFx.ConcurrencyException` (its base is `JasperFx.ConcurrencyException`, a completely separate hierarchy from Marten's *document*-level `Marten.Exceptions.ConcurrentUpdateException`). Map both to a `409 Conflict` centrally — once, in `Program.cs`, not per-handler:

```csharp
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception ex) when (ex is JasperFx.ConcurrencyException or Marten.Exceptions.ConcurrentUpdateException)
    {
        context.Response.Clear();
        await Results.Conflict("This resource was modified by someone else since you last loaded it. Reload and try again.")
            .ExecuteAsync(context);
    }
});
```

**Routing** — `[WolverinePost]` for create/mutate, `[WolverinePut]` if the slice.json models it as idempotent replace. No manual route registration anywhere: Wolverine.Http discovers every `[WolverineGet]`/`[WolverinePost]` handler across all module assemblies automatically via `opts.Discovery.IncludeAssembly(...)` in `Api.Host/Program.cs`.

**Ownership/authorization**: use whatever policy any authenticated caller may act under for broadly-available actions; a role-gated policy for role-restricted actions. Role alone is not enough when the action is scoped to the caller's *own* resource — add an explicit `entity.OwnerId != callerOwnerId → Forbid()` check as shown above; a role check without an ownership check lets any caller with that role act on every other caller's resources, not just their own.

### Existing entity, or new entity?

If `<SliceName>` acts on an entity that doesn't exist yet, create it per this step and register its stream/snapshot config per Step 5. If it's an existing entity (check the module's `Domain` project first), only add whatever new `Apply` method/factory this slice needs — do not add unrelated methods.

---

## Step 4 — Tests first

Write these **before** wiring the handler's business logic, using the slice.json `specifications[]` as your scenario list — one test per specification, at minimum.

### Layer 1 — Domain test (new entity only)

File: `tests/<SolutionName>.Modules.<Context>.Tests/Domain/<Entity>Tests.cs`

xUnit + FluentAssertions, no mocks. Call the factory/domain method directly and assert on resulting state — do **not** test calling a method from an invalid state (entities don't guard preconditions; that's Layer 2's job). If the entity has no public "jump to any state" setter, forcing an arbitrary prior state for a `[Theory]` may require reflection against the private `Apply` methods — a legitimate, if slightly ugly, pattern for testing an entity whose only public surface is its domain methods.

### Layer 2 — Handler test (mocked)

File: `tests/<SolutionName>.Modules.<Context>.Tests/Handlers/<SliceName>HandlerTests.cs`

xUnit + FluentAssertions + NSubstitute. Call `<SliceName>Handler.Handle(...)` directly with a hand-built `ClaimsPrincipal` and a `Substitute.For<IDocumentSession>()`. **`session.Events.FetchForWriting<T>`/`AggregateStreamAsync<T>` return real Marten types that are awkward to mock meaningfully** — if the handler's logic is simple enough that mocking the stream fetch is more trouble than it's worth, prefer Layer 3 (Testcontainers) for that handler instead of fighting the mock. Where mocking is workable:

```csharp
private static ClaimsPrincipal BuildUser(Guid ownerId) =>
    new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

[Fact]
public async Task Handle_WhenXDoesNotExist_ReturnsNotFound()
{
    var session = Substitute.For<IDocumentSession>();
    // set up session.Events... to return a stream with a null Aggregate,
    // or prefer Layer 3 if this is awkward to express

    var result = await <SliceName>Handler.Handle(new <SliceName>Request(...), BuildUser(ownerId), session, CancellationToken.None);

    result.Result.Should().BeOfType<NotFound>();
}
```

Naming convention: `MethodName_Scenario_ExpectedOutcome`. Assert on the `Results<...>` discriminated union's concrete type, and on `session.Received(1).SaveChangesAsync(...)` for the success path.

### Layer 3 — Testcontainers test (real event store)

File: `tests/<SolutionName>.IntegrationTests/<Context>/<SliceName>IntegrationTests.cs`, using a shared per-module Postgres fixture (create `<Context>PostgresFixture.cs` if this module doesn't have one yet: `Testcontainers.PostgreSql`, one container per xUnit collection, `DocumentStore.For(opts => { module.MartenConfiguration.Configure(opts); opts.AutoCreateSchemaObjects = AutoCreate.All; })` — the exact same module Marten config production uses). Call the handler directly against `fixture.Store.LightweightSession()`, exercising the real `FetchForWriting`/`AggregateStreamAsync`/`Query<T>()` path Layer 2 can't reach. This is the layer to reach for whenever Layer 2's mocking gets awkward — event-sourced handlers lean on Layer 3 more than a document-store handler would have, precisely because the stream-fetch APIs aren't friendly to mock.

---

## Step 5 — Register the entity's event stream (and decide on a snapshot)

In `src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/<Context>Module.cs`'s `IMartenModuleConfiguration.Configure`, the module-wide event schema is already set once:

```csharp
options.Events.DatabaseSchemaName = SchemaName;
```

No per-entity registration is *required* beyond that — Marten discovers the stream from `FetchForWriting`/`StartStream`/`AggregateStreamAsync` calls at runtime. The one decision left is whether this entity also needs a persisted **Inline snapshot**:

```csharp
options.Projections.Snapshot<<Entity>>(SnapshotLifecycle.Inline);
```

**Add a snapshot only when something under `ReadModels/**` genuinely queries this entity's current state by id** — a `GetXStatus`/`GetXDetails`-style query handler that does `session.LoadAsync<<Entity>>(id)`, or an ownership check elsewhere that loads it. If nothing queries it, leave it as a bare event stream with no snapshot — there's no cost to paying for a materialized read side nothing reads.

This is a genuinely different kind of decision than the state-computation rule in `build-automation`'s Step 3 (a command/automation's own narrow decision-state is *never* snapshotted, full stop) — don't conflate the two. This step is about the entity's *own* durable identity, which read models are allowed to depend on; that step is about a single handler's private, disposable scratch state, which nothing else should depend on. See `build-automation`'s Step 3 for why persisting or sharing that kind of state specifically causes problems.

Choose `SnapshotLifecycle.Inline` (folded synchronously in the same transaction as the event append, inside the same session Wolverine's outbox uses) over `Async` (a separate daemon, eventually consistent) unless you have a specific reason to decouple write latency from projection cost — for a request/response HTTP API, `Inline` means a caller's next GET always sees their own prior write, with no eventual-consistency window to reason about or test around.

No SQL/Flyway migration file — Marten manages the DDL itself.

---

## Step 6 — Quality checks

```bash
dotnet build <path-to-your-.NET-solution>/<SolutionName>.sln
dotnet test <path-to-your-.NET-solution>/<SolutionName>.sln --filter "FullyQualifiedName~<SliceName>"
```

Run only the slice's own tests, not the full suite.

---

## Files to create

```
src/Modules/<Context>/<SolutionName>.Modules.<Context>.Api/Commands/<SliceName>/
├── <SliceName>.cs           ← request + response records
└── <SliceName>Handler.cs    ← static Handle(...)

src/Modules/<Context>/<SolutionName>.Modules.<Context>.Domain/
├── <Entity>.cs                    ← only if a new entity is needed
└── Events/<Context>Events.cs      ← only if a new event type is needed

tests/<SolutionName>.Modules.<Context>.Tests/
├── Domain/<Entity>Tests.cs               ← Layer 1, new entity only
└── Handlers/<SliceName>HandlerTests.cs   ← Layer 2, where mockable

tests/<SolutionName>.IntegrationTests/<Context>/
└── <SliceName>IntegrationTests.cs        ← Layer 3, where Layer 2 mocking is awkward
```

---

## Final Verification: Does the Implementation Match slice.json?

Before treating this slice as done, verify against slice.json:

- [ ] Every field in `commands[].data` has a corresponding property on `<SliceName>Request` — no invented fields, none missing
- [ ] Every event in `events[]` has a corresponding type, and is reflected in the entity's `Apply` methods — names match exactly
- [ ] Every entry in `specifications[]` maps to a test case (Layer 1/2/3 as appropriate)
- [ ] No business rules, defaults, or constraints were added that do not appear in slice.json `description` or `comments`
- [ ] No field names were assumed or guessed — if a field is not in slice.json, it is not in the code
- [ ] The handler decides only "is this request valid" — any further consequence described in the slice.json belongs in a separate automation slice, not inlined here
- [ ] `<SliceName>Handler` class name ends in `Handler`
- [ ] New/changed entity has `[JsonInclude]`/`[JsonConstructor]`
- [ ] A snapshot was added only if a read model genuinely queries this entity by id — not by default, not "just in case"
- [ ] `dotnet build` and the slice's own tests pass
