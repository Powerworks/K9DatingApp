# Testing Approach

Every slice built so far (Identity, Profiles, Discovery, ShelterAdoption) has
been verified exactly once, by hand, with `curl` against a real running
`Api.Host` and a real Postgres/RabbitMQ (see `docs/05-event-modeling-blueprint.md`
and the live-verification notes that came out of that process). That caught
real bugs — a silently-unregistered Wolverine handler, a projector that never
flushed, FluentValidation never actually running on any Wolverine.Http
endpoint — but it's manual and it evaporates the moment the terminal closes.
Nothing stops a future change from reintroducing any of those same bugs.

This doc defines a permanent, automated replacement (and a few new checks
live verification could never do, like mechanically enforcing module
boundaries). It's a four-layer pyramid. Layers 1–2 are fast and need no
infrastructure; layer 3 needs Docker; layer 4 is a static/reflection check,
not really a "test" of behavior at all.

## Layer 1 — Domain unit tests (no mocks, no infra)

**What:** direct unit tests of entity factory methods and domain methods —
`Application.Submit(...)`, `.StartDraft(...)`, `.EditDetails(...)`, etc. —
called directly, asserting on the resulting object's state.

**Tooling:** xUnit + FluentAssertions. Nothing else. These run in
milliseconds and need no Marten, no Postgres, no Docker.

**A convention specific to this codebase that changes what these tests can
prove:** domain methods here do *not* guard their own preconditions. Look at
`Application.cs` — `Reject(reason)` unconditionally sets
`Status = Rejected` regardless of current status; the "only valid from
UnderReview" rule is enforced by the *handler* (`RejectApplicationHandler`),
not the entity. This is a deliberate, existing pattern (see the `<summary>`
comments throughout `Application.cs`: "State-guard ... lives in the
handler"). It means Layer 1 tests can only prove "calling this method
produces this state change" — they cannot prove "calling this method from
the wrong state is rejected," because the entity itself doesn't reject
anything. That guarantee is a Layer 2 concern. Don't write a Layer 1 test
that asserts a domain method throws or no-ops on bad state — it doesn't, and
that's correct per this codebase's own design, not a gap to "fix" while
writing tests.

**Where:** `tests/K9Crush.Modules.<Module>.Tests/Domain/`, one test class
per entity, mirroring `src/Modules/<Module>/K9Crush.Modules.<Module>.Domain/`.

## Layer 2 — Handler unit tests (mocks)

**What:** call a slice's static `Handle(...)` method directly — every
handler in this codebase already takes its dependencies as explicit method
parameters (`IDocumentSession`, `ClaimsPrincipal`, etc.), so there's no
DI container or HTTP pipeline needed to exercise the actual branching logic:
`NotFound`, `Forbid` (ownership check), `Conflict` (state guard), success.
This is where the state-guard behavior noted in Layer 1 actually gets
verified.

**Tooling:** xUnit + FluentAssertions + **NSubstitute** (newly added to
`Directory.Packages.props` — no mocking library was pinned before this).
Build a `ClaimsPrincipal` by hand (a `ClaimsIdentity` with a
`NameIdentifier` claim is enough to satisfy every handler's
`Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!)` pattern — no
real JWT needed for this layer, unlike the old curl-based verification).

**The hard limit of this layer — read before mocking `IDocumentSession`:**
`session.LoadAsync<T>(id, ct)`, `session.Store(entity)`, and
`session.SaveChangesAsync(ct)` are plain interface members and mock cleanly.
`session.Query<T>().Where(...).ToListAsync(ct)` does **not** — Marten's LINQ
provider (`IMartenQueryable<T>`) is not something NSubstitute (or any mock
library) can fake meaningfully; `Query<T>()` returning a bare `IQueryable<T>`
substitute will not support Marten's async extension methods the way real
Marten does. **Do not attempt to mock a handler that calls
`session.Query<T>()`.** Check every handler before writing a Layer 2 test
for it — if it only calls `LoadAsync`/`Store`/`SaveChangesAsync`, it belongs
here (e.g. `EditApplicationDetailsHandler`, `ResumeDraftApplicationHandler`).
If it calls `Query<T>()` (e.g. `SubmitApplicationHandler`,
`StartDraftApplicationHandler`, every `GetX` read model), it belongs in
Layer 3 instead.

**Where:** `tests/K9Crush.Modules.<Module>.Tests/Handlers/`, one test class
per handler, mirroring `src/.../Api/Commands|ReadModels|Automations/`.

## Layer 3 — Integration tests against real Postgres (Testcontainers)

**What:** for any handler that touches `session.Query<T>()`, or that needs
to prove round-trip Marten serialization actually works (the exact class of
bug `DogProfile`'s missing `[JsonConstructor]`/`[JsonInclude]` was — a mock
would never have caught that, only a real `LoadAsync` against a real
document store would), spin up a real disposable Postgres via
`Testcontainers.PostgreSql` (already pinned in `Directory.Packages.props` —
this was clearly the original scaffold's intent even though nothing used it
yet), configure a real Marten `DocumentStore` against it the same way each
module's `<Module>Module.cs` does, and call the handler for real.

**Tooling:** xUnit + FluentAssertions + `Testcontainers.PostgreSql`. One
container per test collection (`IAsyncLifetime` fixture), not per test —
container startup is seconds, not milliseconds, so share it and rely on
distinct random IDs per test rather than paying that cost repeatedly.

**This is the durable, automated replacement for `curl`-based live
verification** of anything involving a real query — including the drafts
feature's trickiest logic (`StartDraftApplicationHandler`'s 3-draft limit,
`SubmitApplicationHandler`'s draft-graduation branch), both of which read
back the applicant's full application list via `Query<T>().Where(...)`
before deciding what to do.

**What Layer 3 deliberately does not cover:** RabbitMQ delivery, `[Authorize]`
policy enforcement, or DataAnnotations validation — those live outside the
handler method itself (in `Program.cs` middleware/policy wiring), see Layer
4's honest gap below.

**Where:** `tests/K9Crush.IntegrationTests/`, organized by module, not
mirroring the fine-grained per-slice split of Layers 1–2 — a shared
container fixture is the point.

## Layer 4 — Architecture fitness tests

**What:** mechanical, reflection-based checks that a manual review can
forget to do, run on every build. `docs/05-event-modeling-blueprint.md`
Section 6 wrote several of these down as "still pending" before any test
project existed; `K9Crush.ArchitectureTests` now implements the two that
are both mechanically checkable *and* map directly to real bugs found this
session:

1. **Module boundary rule** (NetArchTest): every module's `.Domain` assembly
   must depend on `K9Crush.BuildingBlocks.Domain` only, never another
   module's `Domain`/`Api`/`Contracts` assembly.
2. **Entity serialization rule** (plain reflection): every non-abstract type
   deriving from `Entity` with a non-public parameterless constructor must
   have `[JsonConstructor]` on it, and every property with a non-public
   setter must have `[JsonInclude]` — this is exactly the `DogProfile` bug
   from Section 6.1 of the blueprint, mechanized so it can't come back
   silently on a new entity.
3. **Handler naming rule** (plain reflection): every class with a public
   `static` method literally named `Handle` must have a class name ending in
   `Handler` — this is exactly the `DogProfileCreatedProjector` /
   Wolverine-silent-discovery bug from blueprint Section 5.2, mechanized.

**What's explicitly deferred, and why:** the blueprint's other proposed
rules ("no `Commands/**` type branches on another module's event",
"no `Projections.Snapshot<T>` loaded inside `Commands/**`/`Automations/**`")
need call-site/semantic analysis ("branches on" vs. "produces"), not just
type-level dependency graphs — NetArchTest operates on Mono.Cecil-derived
type dependencies, which can tell you *that* a type references another
type, not *how* it uses it. Revisit if a violation of one of these actually
happens and it's worth the investment; don't build the mechanism
speculatively ahead of a real incident.

## What's out of scope for now

**Full HTTP-pipeline endpoint tests** (spinning up `Api.Host` in-memory via
`WebApplicationFactory`/Alba and hitting real routes) aren't set up — no
package for it is pinned, and it's a bigger lift (real `[Authorize]` policy
evaluation needs a real or faked JWT bearer handler). Manual `curl`-based
live verification remains the stand-in for "does the route actually wire up,
does `[Authorize]` actually gate it, does DataAnnotations actually 400" —
revisit this if/when it's worth adding Alba as a fifth layer.

## Project layout

```
tests/
  K9Crush.ArchitectureTests/            (Layer 4, solution-wide)
  K9Crush.Modules.ShelterAdoption.Tests/  (Layers 1–2, one project per module)
    Domain/
    Handlers/
  K9Crush.IntegrationTests/              (Layer 3, cross-module, Testcontainers)
```

One test project per module (not per slice) for Layers 1–2, mirroring the
`src/Modules/<Module>/` split so module boundaries stay visible in the test
tree too — a `ShelterAdoption.Tests` project should never reference
`Identity.Domain`, same rule as production code.

## Naming convention

`MethodName_Scenario_ExpectedOutcome`, e.g.
`SubmitDraft_WhenCalled_SetsStatusToPendingAndSubmittedAt`,
`Handle_WhenApplicationNotOwnedByCaller_ReturnsForbid`.

## Current coverage (as of this doc's creation)

Only `ShelterAdoption`'s `Application` entity and its two simplest
LoadAsync/Store-only handlers (`EditApplicationDetailsHandler`,
`ResumeDraftApplicationHandler`) have Layer 1/2 tests, plus one Layer 3
integration test covering the drafts feature's LINQ-query paths
(`StartDraftApplicationHandler`, `SubmitApplicationHandler`'s
graduation branch). Everything else built so far (Identity, Profiles,
Discovery, and the rest of ShelterAdoption) has **no automated test
coverage yet** — only the original one-time manual `curl` verification.
Filling that in is follow-up work, one module at a time, same as the
slices themselves were built.
