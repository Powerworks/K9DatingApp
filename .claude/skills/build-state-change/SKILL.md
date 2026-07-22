---
name: build-state-change
description: Implements a Wolverine.Http + Marten state-change slice (request/response, handler, tests) from a slice.json definition
---

# Build State Change Slice

> Before doing anything else, read the slice definition from `build-kit-dotnet/.slices/{Context}/{slicename}/slice.json`. This file is the **source of truth** for all fields, events, and metadata. Never invent fields not defined there. If you haven't already, run the `load-slice` skill first to make sure this file is fresh.

**Write the tests before (or alongside) the handler, not after.** This mirrors `TestingApproach/TestingApproach.md` in the target project (`code/K9Crush-scaffold/K9Crush/`) — Layer 1/2 tests below describe the behavior you're about to build; they should exist and fail (or not compile) before `<CommandName>Handler.cs` does.

---

## What a State Change Slice is

A state-change slice processes a command:
1. Loads whatever state it needs to validate the command (a document, or replayed events)
2. Validates the command against that state
3. Persists the result (stores a document, or appends event(s)) and returns a response

Per `docs/05-event-modeling-blueprint.md` Section 1: a state-change slice may only decide "is this request valid" — never "what else should happen as a consequence beyond emitting/storing my own result." If the slice.json's `description`/`comments` describe a *further* consequence (e.g. "...and if this creates a mutual match, notify both owners"), that further consequence is a separate **automation** slice (see the `build-automation` skill), triggered by the event this slice produces — do not build it inline here. This exact mistake was made once in this codebase (the original `SwipeOnDog` handler) and had to be split apart; don't repeat it.

---

## Step 1 — Read the slice.json

From the slice definition, extract:
- **sliceName** — the slice title (becomes the Command/request name)
- **context** — the bounded context → maps to a module (`Identity`, `Profiles`, `Discovery`, `ShelterAdoption`, or a new one)
- **commands[]** — list of commands with their data fields
- **events[]** — list of events emitted by each command
- **specifications[]** — test scenarios (given/when/then)

> **Comments & description**: each element (commands, events, readmodels, processors, screens, tables) carries a `comments: string[]` array (board comments on that node) and a `description` field; the slice itself also has `comments: string[]`. Use these as implementation hints — pass them as code doc-comments, or validation logic where they add value. When done, resolve each used comment: `POST <BASE_URL>/api/org/<ORG_ID>/boards/<BOARD_ID>/nodes/<nodeId>/comments/<commentId>/resolve` (get comment IDs first via GET on the same path without the last two segments — see `connect`/`load-slice` for `TOKEN`/`BASE_URL`/etc.).

---

## Step 2 — Pick a storage strategy: document vs. event-sourced

Unlike the platform's own emmett-based implementation (always event-sourced), this target app uses **both** patterns, chosen per module, not per slice:

| Module | Strategy |
|---|---|
| Identity, Profiles, ShelterAdoption (and any brand-new module, by default) | **Document-store** — `IDocumentSession.Store`/`LoadAsync` |
| Discovery (and any module the slice.json explicitly ties to swipe/match-style history) | **Event-sourced** — `session.Events.Append` + a live-computed `[CommandName]State` |

Check `src/Modules/<Context>/K9Crush.Modules.<Context>.Api/<Context>Module.cs` for the module's existing `IMartenModuleConfiguration`: if it registers `options.Schema.For<T>()` calls for documents, this module is document-store; if it only sets `options.Events.DatabaseSchemaName`, it's event-sourced. A brand-new module with no existing slices defaults to **document-store** — only choose event-sourced if the slice.json's `specifications[]` genuinely require replaying prior events to decide (e.g. "has this pair already matched"), per ADR-019's guidance that command state should be minimal and computed live, never a shared persisted snapshot.

Go to **Step 3a** for document-store, **Step 3b** for event-sourced. Either way, continue with Step 4 onward.

---

## Step 3a — Document-store pattern

Files: `src/Modules/<Context>/K9Crush.Modules.<Context>.Api/Commands/<SliceName>/`

### `<SliceName>.cs` — request + response records

```csharp
using System.ComponentModel.DataAnnotations;

namespace K9Crush.Modules.<Context>.Api.Commands.<SliceName>;

public sealed record <SliceName>Request(
    [property: Required, MaxLength(50)] string SomeField,
    // ...one property per command data field in slice.json, with
    // [Required]/[MaxLength]/[Range]/etc. matching the field's constraints
    );

public sealed record <SliceName>Response(Guid <Entity>Id /* , ...other fields the caller needs back */);
```

If a request field needs a rule plain attributes can't express (a `Guid` that must not be `Guid.Empty`, a cross-field rule, "can't target itself"), implement `IValidatableObject` instead/additionally — see `SwipeOnDogRequest` (`src/Modules/Discovery/.../Commands/SwipeOnDog/SwipeOnDog.cs`) for the pattern:

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

**Do not** write a separate `AbstractValidator<T>`/FluentValidation class — `[WolverinePost]`/`[WolverineGet]` endpoints bypass Wolverine's message-bus validation pipeline entirely (confirmed live in this codebase: FluentValidation never ran, an invalid body 500'd instead of 400ing). Validation lives on the request record itself; `Program.cs`'s `opts.UseDataAnnotationsValidationProblemDetailMiddleware()` wires it centrally — no per-slice registration needed.

### `<SliceName>Handler.cs` — the handler

```csharp
using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.<Context>.Domain;
using Wolverine.Http;

namespace K9Crush.Modules.<Context>.Api.Commands.<SliceName>;

public static class <SliceName>Handler
{
    [WolverinePost("/api/v1/<context-kebab>/<slice-kebab-route>")]
    [Authorize(Policy = "VerifiedOwner")] // or "Shelter"/"Admin" — see Step 4
    public static async Task<Results<Ok<<SliceName>Response>, NotFound, ForbidHttpResult, Conflict<string>>> Handle(
        <SliceName>Request request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var entity = await session.LoadAsync<TargetEntity>(request.SomeId, cancellationToken);
        if (entity is null)
            return TypedResults.NotFound();

        if (entity.OwnerId != callerOwnerId) // only if the slice needs an ownership check — see Step 4
            return TypedResults.Forbid();

        // business rule checks from slice.json specifications[] — return
        // Conflict<string> for anything the slice.json calls out as a
        // named error scenario (SPEC_ERROR), NotFound/Forbid otherwise

        // mutate/create the entity via its own domain method or factory —
        // never set properties directly from the handler
        var updated = entity.SomeDomainMethod(...);
        session.Store(updated);
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new <SliceName>Response(updated.Id));
    }
}
```

**The class must be named `<SliceName>Handler`** — Wolverine's convention-based discovery only recognizes a `Handle` method if the containing class name ends in `Handler`. This is not optional; a correctly-implemented `Handle` method in a class named anything else is silently never registered (this bit a real projector in this codebase — see the `build-state-view` skill for the full incident).

**Routing** — `[WolverinePost]` for create/mutate, `[WolverinePut]` if the slice.json models it as idempotent replace. No manual route registration anywhere: Wolverine.Http discovers every `[WolverineGet]`/`[WolverinePost]` handler across all module assemblies automatically via `opts.Discovery.IncludeAssembly(...)` in `Api.Host/Program.cs` (already wired for every module) — nothing to add there for a new slice in an existing module.

**Ownership/authorization**: use `[Authorize(Policy = "VerifiedOwner")]` for anything any authenticated, email-verified owner may do; `"Shelter"`/`"Admin"` for role-gated actions (ADR-017, `RoleRequirement`/`RoleAuthorizationHandler` in `BuildingBlocks.Web`). Role alone is not enough when the action is scoped to the caller's *own* resource — add an explicit `entity.OwnerId != callerOwnerId → Forbid()` check as shown above (see `AddDogListingHandler` for the worked example: `Shelter` policy *and* an ownership check, since role alone would let any shelter manage any other shelter's listings).

### Existing entity, or new entity?

If `<SliceName>` acts on an entity that doesn't exist yet, create it in `src/Modules/<Context>/K9Crush.Modules.<Context>.Domain/<Entity>.cs` per Step 4 below and register its Marten schema per Step 6. If it's an existing entity (check the module's `Domain` project first), only add whatever new factory/domain method this slice needs — do not add unrelated methods.

---

## Step 3b — Event-sourced pattern

Files: same folder convention as 3a, but no persisted entity.

### `<SliceName>.cs` — same as Step 3a (request/response records, DataAnnotations/`IValidatableObject`)

### `<SliceName>Handler.cs`

```csharp
using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using K9Crush.Modules.<Context>.Domain;
using K9Crush.Modules.<Context>.Domain.Events;
using Wolverine.Http;

namespace K9Crush.Modules.<Context>.Api.Commands.<SliceName>;

public static class <SliceName>Handler
{
    [WolverinePost("/api/v1/<context-kebab>/<slice-kebab-route>")]
    [Authorize(Policy = "VerifiedOwner")]
    public static async Task<Results<Ok<<SliceName>Response>, NotFound, ForbidHttpResult>> Handle(
        <SliceName>Request request,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var callerOwnerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ownership check against a read model / document this module
        // already has, the same way SwipeOnDogHandler checks against its
        // own DiscoveryFeedItem rather than trusting an id from the body

        var streamId = <StreamKeyHelper>.IdFor(request.SomeId /* , ... */);
        var now = DateTimeOffset.UtcNow;

        session.Events.Append(streamId, new <EmittedEvent>(request.SomeId, now /* , ... */));
        await session.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new <SliceName>Response(Acknowledged: true));
    }
}
```

Do **not** load a persisted snapshot to validate the command (no `Projections.Snapshot<T>()`, no `LoadAsync<SharedAggregate>`) — per ADR-019, if the command genuinely needs to know prior stream history to decide, compute a minimal, single-purpose `<SliceName>State` live via `session.Events.AggregateStreamAsync<T>(streamId, ...)`, following the `build-automation` skill's Step 3 pattern (`DetectMutualMatchState`/`DetectMutualMatchHandler`). Never share that state type with another command or automation — a second command needing "similar-looking" state gets its own type.

If the stream identity is derived from more than one id (e.g. an unordered pair), add a small deterministic helper like `MatchStream.IdFor` (`src/Modules/Discovery/K9Crush.Modules.Discovery.Domain/MatchStream.cs`) rather than inlining hashing logic in the handler.

New event types go in `src/Modules/<Context>/K9Crush.Modules.<Context>.Domain/Events/<Context>Events.cs` as `sealed record`s implementing `IDomainEvent` (`BuildingBlocks.Domain`):

```csharp
public sealed record <EmittedEvent>(Guid SomeId, DateTimeOffset OccurredAt) : IDomainEvent;
```

---

## Step 4 — Entity serialization (document-store only)

If Step 3a's entity is new and restricts its own constructor/setters (the normal DDD instinct — only factory methods produce a valid instance), it needs `[JsonInclude]`/`[JsonConstructor]` or Marten's `System.Text.Json`-based serializer cannot deserialize it back out (`session.Store` works fine either way; `LoadAsync`/`Query` throw `NotSupportedException` on the very first real read):

```csharp
public class <Entity> : Entity
{
    [JsonInclude] public Guid OwnerId { get; private set; }
    // ...every non-public-setter property needs [JsonInclude]

    [JsonConstructor]
    private <Entity>() { }

    public static <Entity> Create(...) { ... }
}
```

Entities do **not** guard their own preconditions in this codebase (e.g. no `if (Status != X) throw` inside a domain method) — that guard belongs in the handler (Step 3a), not the entity. Keep new domain methods consistent with this: they should set state unconditionally and let the handler decide whether calling them is currently valid.

---

## Step 5 — Tests first

Per `TestingApproach/TestingApproach.md`. Write these **before** wiring the handler's business logic, using the slice.json `specifications[]` as your scenario list — one test per specification, at minimum.

### Layer 1 — Domain test (document-store, new entity only)

File: `tests/K9Crush.Modules.<Context>.Tests/Domain/<Entity>Tests.cs`

xUnit + FluentAssertions, no mocks. Call the factory/domain method directly and assert on resulting state — do **not** test calling a method from an invalid state (entities don't guard preconditions; that's Layer 2's job). Follow `ApplicationTests.cs` (`tests/K9Crush.Modules.ShelterAdoption.Tests/Domain/ApplicationTests.cs`) for the pattern, including the reflection trick for forcing an arbitrary state in a `[Theory]` if the entity has no public "jump to any state" setter.

### Layer 2 — Handler test (mocked)

File: `tests/K9Crush.Modules.<Context>.Tests/Handlers/<SliceName>HandlerTests.cs`

xUnit + FluentAssertions + NSubstitute. Call `<SliceName>Handler.Handle(...)` directly with a hand-built `ClaimsPrincipal` and a `Substitute.For<IDocumentSession>()`. **Only write this layer if the handler restricts itself to `LoadAsync`/`Store`/`SaveChangesAsync`/`Events.Append`** — `session.Query<T>()` (Marten's `IMartenQueryable<T>`) cannot be meaningfully mocked by NSubstitute; a handler that queries needs Layer 3 instead (or in addition, for the non-query branches). Follow `EditApplicationDetailsHandlerTests.cs` (`tests/K9Crush.Modules.ShelterAdoption.Tests/Handlers/`) for the pattern:

```csharp
private static ClaimsPrincipal BuildUser(Guid ownerId) =>
    new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ownerId.ToString())]));

[Fact]
public async Task Handle_WhenXDoesNotExist_ReturnsNotFound()
{
    var session = Substitute.For<IDocumentSession>();
    session.LoadAsync<TargetEntity>(id, Arg.Any<CancellationToken>()).Returns((TargetEntity?)null);

    var result = await <SliceName>Handler.Handle(new <SliceName>Request(...), BuildUser(ownerId), session, CancellationToken.None);

    result.Result.Should().BeOfType<NotFound>();
}
```

Naming convention: `MethodName_Scenario_ExpectedOutcome`. Assert on the `Results<...>` discriminated union's concrete type, and on `session.Received(1).Store(...)`/`SaveChangesAsync(...)` for the success path.

### Layer 3 — Testcontainers test (only if the handler uses `session.Query<T>()`)

File: `tests/K9Crush.IntegrationTests/<Context>/<SliceName>IntegrationTests.cs`, using a shared per-module Postgres fixture (create `<Context>PostgresFixture.cs` if this module doesn't have one yet — `ShelterAdoptionPostgresFixture.cs` is the reference: `Testcontainers.PostgreSql`, one container per xUnit collection, `DocumentStore.For(opts => { ...; module.MartenConfiguration.Configure(opts); opts.AutoCreateSchemaObjects = AutoCreate.All; })` — the exact same module Marten config production uses). Follow `DraftsIntegrationTests.cs` for the pattern: call the handler directly against `fixture.Store.LightweightSession()`, exercising the real LINQ query path Layer 2 can't reach.

---

## Step 6 — Register the entity's Marten schema (document-store, new entity only)

In `src/Modules/<Context>/K9Crush.Modules.<Context>.Api/<Context>Module.cs`'s `IMartenModuleConfiguration.Configure`:

```csharp
options.Schema.For<<Entity>>()
    .DatabaseSchemaName(SchemaName)
    .Identity(x => x.Id)
    .Index(x => x.OwnerId); // index whatever fields future queries will filter on
```

No Flyway/SQL migration file — Marten manages the DDL itself (`AutoCreateSchemaObjects`, currently `CreateOrUpdate` in Development per `Program.cs`'s own TODO comment). This is a real difference from the node build-kit's `supabase/migrations/V{N}__*.sql` step — do not create a migration file for this.

For event-sourced slices, no schema registration is needed beyond the module's existing `options.Events.DatabaseSchemaName = SchemaName` (already set if the module is event-sourced at all).

---

## Step 7 — Quality checks

```bash
dotnet build code/K9Crush-scaffold/K9Crush/K9Crush.sln
dotnet test code/K9Crush-scaffold/K9Crush/K9Crush.sln --filter "FullyQualifiedName~<SliceName>"
```

Run only the slice's own tests, not the full suite. If checks pass, this slice is ready — `code/K9Crush-scaffold/K9Crush/CLAUDE.md` covers committing and updating slice status as the final step of the overall build flow.

---

## Files to create

```
src/Modules/<Context>/K9Crush.Modules.<Context>.Api/Commands/<SliceName>/
├── <SliceName>.cs           ← request + response records
└── <SliceName>Handler.cs    ← static Handle(...)

src/Modules/<Context>/K9Crush.Modules.<Context>.Domain/
└── <Entity>.cs               ← only if a new entity is needed (document-store)
└── Events/<Context>Events.cs ← only if a new event type is needed (event-sourced)

tests/K9Crush.Modules.<Context>.Tests/
├── Domain/<Entity>Tests.cs           ← Layer 1, document-store new entity only
└── Handlers/<SliceName>HandlerTests.cs  ← Layer 2

tests/K9Crush.IntegrationTests/<Context>/
└── <SliceName>IntegrationTests.cs    ← Layer 3, only if session.Query<T>() is used
```

---

## Final Verification: Does the Implementation Match slice.json?

Before treating this slice as done, verify against slice.json:

- [ ] Every field in `commands[].data` has a corresponding property on `<SliceName>Request` — no invented fields, none missing
- [ ] Every event in `events[]` has a corresponding type (event-sourced) or is reflected in the entity's resulting state (document-store) — names match exactly
- [ ] Every entry in `specifications[]` maps to a test case (Layer 1/2/3 as appropriate)
- [ ] No business rules, defaults, or constraints were added that do not appear in slice.json `description` or `comments`
- [ ] No field names were assumed or guessed — if a field is not in slice.json, it is not in the code
- [ ] The handler decides only "is this request valid" — any further consequence described in the slice.json belongs in a separate automation slice, not inlined here
- [ ] `<SliceName>Handler` class name ends in `Handler`
- [ ] New/changed entity has `[JsonInclude]`/`[JsonConstructor]` if it restricts its own setters/constructor
- [ ] `dotnet build` and the slice's own tests pass
