# Event Modeling Blueprint — K9Crush Platform

> **Sections 2 and 3 are historical** — they walk through the original `SwipeOnDog`/Discovery-era incident and a slice inventory for modules (Discovery, Chat, Subscriptions, Moderation, etc.) since deleted in the 2026-07-24 product descope or never built. The *lesson* those sections teach (a command slice may only decide "is this request valid," never "what else should happen as a consequence") is still exactly right and still enforced — only the worked example is out of date. Sections 4, 5, and 6 below are kept current against the live app and reflect **ADR-031** (event sourcing adopted as the default persistence model for every module, superseding the old per-module Document-vs-Event-sourced split this doc originally assumed) — see `docs/03-solution-architecture.md` Section 2.1/2.2/10 for the full decision.

## 1. What's Changing
Every feature slice in the codebase is now exactly one of three types. A slice never mixes types — if a feature needs both a command and a read model, that's two slices.

| Lane | Shape | Wolverine mechanism |
|---|---|---|
| **State-change** | `SCREEN/CALLER → COMMAND → EVENT(s)` | `[WolverinePost]`/`[WolverinePut]` HTTP endpoint, appends event(s) or stores a document, no branching on "what should also happen elsewhere" |
| **State-view** | `EVENT(s) → READMODEL → SCREEN/CALLER` | `[WolverineGet]` HTTP endpoint reading a Marten document/projection; the projection itself is built by a handler that reacts to the relevant event(s) |
| **Automation** | `EVENT(s) → AUTOMATION → COMMAND → EVENT(s)` | A local Wolverine handler triggered by a domain event (via Marten event forwarding — see Section 4), which makes a decision and appends further event(s)/cascades an integration event |

This is a stricter version of "vertical slice architecture" than what was originally scaffolded — it's vertical slicing with an explicit rule for *where a decision is allowed to live*. A command slice is allowed to decide "is this request valid," never "what else should happen as a consequence beyond emitting my own event."

## 2. The Bug This Caught
The original `SwipeOnDog` handler did two things: (1) record the swipe, and (2) decide whether it caused a mutual match and, if so, append `MatchFormed` and publish `MatchCreatedV1`. That's a command slice quietly containing an automation.

**Before:**
```
SwipeOnDog command → appends DogLiked/DogPassed
                    → [inline] checks reverse-like
                    → [inline] appends MatchFormed
                    → [inline] cascades MatchCreatedV1
```

**After:**
```
SwipeOnDog command        → appends DogLiked/DogPassed only. Returns {Acknowledged: true}.
DetectMutualMatch automation → triggered BY the DogLiked event
                              → checks reverse-like against the aggregate
                              → appends MatchFormed
                              → cascades MatchCreatedV1
```

This has already been applied to the scaffold: `Commands/SwipeOnDog/` now only appends the swipe event, and a new `Automations/DetectMutualMatch/` handler owns the match decision, triggered by Marten forwarding the `DogLiked` domain event straight to a local Wolverine handler.

### UX consequence — please confirm this is acceptable
`SwipeOnDogResponse` no longer tells the caller whether the swipe produced a match. That information now arrives asynchronously — either via the `MatchCreatedV1` → Notifications automation pushing a SignalR toast, or by the client polling a match read model. This is the *correct* shape under this discipline (the command slice can't know the automation's outcome without becoming the automation), but it's a real product/UX decision: the swipe screen needs to handle "match!" as a follow-up event rather than an immediate response. If instant in-request feedback turns out to be a hard product requirement, the alternative is to keep the automation but have it publish a message the *same HTTP request* awaits before responding — which reintroduces coupling between the two slices and is worth an explicit ADR if you go that way rather than sliding back into it by habit.

## 3. Full Slice Inventory

Legend: **C** = state-change (command), **V** = state-view (read model), **A** = automation.

| Module | Slice | Lane | Trigger | Produces |
|---|---|---|---|---|
| Identity | RegisterOwner | C | HTTP | `OwnerRegistered` |
| Identity | VerifyEmail | C | HTTP | `OwnerVerified` |
| Identity | OwnerAccountView | V | `OwnerRegistered`, `OwnerVerified` | — |
| Identity | ProvisionOwnerOnSupabaseSignup | A | Supabase user-created webhook/event | `OwnerRegistered` *(bridges external IdP event into our stream — see note below)* |
| Profiles | **CreateDogProfile** ✅ scaffolded | C | HTTP | `DogProfileCreatedV1` |
| Profiles | UpdateDogProfile | C | HTTP | `DogProfileUpdated` |
| Profiles | **GetDogProfile** ✅ scaffolded | V | HTTP query (direct document read) | — |
| Profiles | AttachPhotoToProfile | A | `MediaUploaded` (from Media module) | `DogProfileUpdated` |
| Discovery | **SwipeOnDog** ✅ scaffolded, corrected | C | HTTP | `DogLiked` / `DogPassed` |
| Discovery | **DetectMutualMatch** ✅ scaffolded, new | A | `DogLiked` | `MatchFormed`, `MatchCreatedV1` |
| Discovery | **GetDiscoveryFeed** ✅ scaffolded | V | HTTP query + `DogProfileCreatedV1` (builds the read model) | — |
| Discovery | UndoLastSwipe | C | HTTP | `SwipeUndone` |
| Chat | SendMessage | C | HTTP/SignalR invoke | `MessageSent` |
| Chat | CreateConversationOnMatch | A | `MatchCreatedV1` | `ConversationCreated` |
| Chat | ConversationTranscript | V | `MessageSent`, `ConversationCreated` | — |
| Chat | MarkAsRead | C | HTTP/SignalR invoke | `MessageRead` |
| Notifications | NotifyOnMatch | A | `MatchCreatedV1` | `NotificationSent` |
| Notifications | NotifyOnMessage | A | `MessageSent` (debounced by presence check) | `NotificationSent` |
| Notifications | UpdateNotificationPreferences | C | HTTP | `NotificationPreferencesUpdated` |
| Notifications | NotificationHistory | V | `NotificationSent` | — |
| Media | RequestUploadUrl | C | HTTP | `UploadUrlIssued` |
| Media | ConfirmUpload | C | HTTP (client callback after direct-to-storage upload) | `MediaUploaded` |
| Media | ScanUploadedMedia | A | `MediaUploaded` | `MediaApproved` / `MediaRejected` |
| Subscriptions | Subscribe | C | HTTP | `SubscriptionChanged` |
| Subscriptions | Entitlements | V | `SubscriptionChanged` | — |
| Moderation | ReportUserOrContent | C | HTTP | `ContentFlagged` |
| Moderation | BanUser | C | HTTP (admin action) | `UserBanned` |
| Moderation | RevokeAccessOnBan | A | `UserBanned` | *(no new event — calls Supabase's admin API to revoke sessions)* |
| Moderation | ModerationQueue | V | `ContentFlagged` | — |

Only Identity/Profiles/Discovery are scaffolded so far; the rest of the table is the target shape for when those modules get built, so the pattern is consistent from the start rather than retrofitted.

> **Identity note:** since Supabase now owns registration (ADR-005), `RegisterOwner`/`OwnerRegistered` most likely becomes purely reactive — an automation bridging Supabase's own user-lifecycle event into our domain — rather than a command our own UI calls directly. Worth confirming once the Identity module is actually built, since it changes whether `RegisterOwner` is a C or an A. Also worth confirming *how* Supabase surfaces that lifecycle event to us — Supabase supports Postgres webhooks/triggers on `auth.users`, which is the most likely mechanism, but this hasn't been verified against a real project.

## 4. How Each Lane Maps to Wolverine + Marten (mechanically)

**Every entity is a self-aggregating event-sourced aggregate (ADR-031)**: a plain class with `Create(TEvent)` + `Apply(TEvent)` overloads. Domain methods build the event, call `Apply` on themselves, and return the event — this is what both `FetchForWriting`/`AggregateStreamAsync` (write side) and, where registered, `Projections.Snapshot<T>(SnapshotLifecycle.Inline)` (read side, see Section state-view below) replay against.

**State-change (command):**
```csharp
[WolverinePost("/api/v1/shelter-adoption/applications/{applicationId:guid}/reject")]
public static async Task<Results<Ok<ApplicationRejectedResponse>, NotFound, Conflict<string>>> Handle(
    Guid applicationId, RejectApplicationRequest request, IDocumentSession session, CancellationToken ct)
{
    var stream = await session.Events.FetchForWriting<Application>(applicationId, ct);
    var application = stream.Aggregate;
    if (application is null) return TypedResults.NotFound();
    if (application.Status != ApplicationStatus.UnderReview)
        return TypedResults.Conflict($"Cannot reject an application in status {application.Status}.");

    var @event = application.Reject(request.Reason);
    stream.AppendOne(@event);
    await session.SaveChangesAsync(ct);

    return TypedResults.Ok(new ApplicationRejectedResponse(application.Id, application.Status));
}
```
`FetchForWriting<T>` fetches the current aggregate and stages optimistic-concurrency-checked appends in one call — this replaces the old `LoadAsync<T>`/`session.Store(entity)` pair everywhere. One command in, event(s) appended, done. No cascaded integration events unless the event *is* the direct, unconditional consequence of the command (e.g. `AddDogListing` → `DogListingAddedV1` — that's not a hidden decision, it's the command's own result).

**State-view (read model):**
```csharp
[WolverineGet("/api/v1/shelter-adoption/applications/{applicationId:guid}")]
public static async Task<Results<Ok<ApplicationStatusResponse>, NotFound>> Handle(Guid applicationId, IQuerySession session, ...)
    // session.LoadAsync<Application>(applicationId, ct) — unchanged call site
```
For a "current state by id/simple filter" read model, the default (per ADR-031) is registering the **same self-aggregating class** as its own `Projections.Snapshot<T>(SnapshotLifecycle.Inline)` in `<Module>Module.cs` — the query handler's `LoadAsync<T>`/`Query<T>()` call site doesn't change at all, it's now reading a projection-maintained document instead of one raw-`Store()`'d by a command. A distinct `<TriggerEvent>ProjectorHandler` building a separately-shaped read-model type is still the right call when the view genuinely differs from "this entity's own current state" (cross-module projections especially — see Section 5.2) or where reusing the write-side class for reads risks the confusion flagged in Section 6.

**Automation:**
```csharp
public static class CancelApplicationsForRemovedListingHandler
{
    public static async Task Handle(DogListingRemovedV1 domainEvent, IDocumentSession session, ...)
    {
        var affected = await session.Query<Application>()
            .Where(x => x.DogListingId == domainEvent.DogListingId && x.IsOpen)
            .ToListAsync();
        // decide, then act — one FetchForWriting/AppendOne pair per affected Application
    }
}
```
Per ADR-028 (unchanged by ADR-031), same-module cascades route through the same shared `k9crush.events` RabbitMQ exchange as genuinely cross-module events — this codebase has no separate in-process "local-only" pub/sub mechanism, so an automation reacting to its own module's event still arrives as an integration event, not via Marten's `SubscribeToEvent<T>` same-process forwarding.

**Command state, ADR-019 (see Solution Architecture doc Section 2.2 for the full writeup, updated for ADR-031):** state used to *decide* a command must be loaded live via `AggregateStreamAsync<T>` against a minimal, never-shared, never-persisted `[CommandName]State` type — never a shared persisted snapshot, and never the same type a read model also queries. This rule is unchanged by ADR-031, but its enforcement gets *harder*: since the write-side aggregate and a read-side `Inline` snapshot are now commonly the same class, `session.LoadAsync<Application>()` (wrong, reads the snapshot) and `session.Events.FetchForWriting<Application>()` (right, decides+appends) both compile and both reference the identical type — see Section 6's new fitness-test rule for how this gets caught mechanically.

A command validating against a *population* of other entities (not a persisted snapshot of itself) is not this rule's concern — `session.Query<Application>()` for a cross-stream check (e.g. an applicant's open-application count) is the same pattern query read models already use, unchanged from before ADR-031.

## 5. Folder Convention (now enforced in the scaffold)
```
K9Crush.Modules.<Module>.Domain/
└── Events/
    └── <Entity>Events.cs            (one sealed record per transition, <Entity><PastTenseVerb>V1 -
                                       new as of ADR-031; every entity was a plain document before)

K9Crush.Modules.<Module>.Api/
├── Commands/
│   └── <CommandName>/
│       ├── <CommandName>.cs         (request + response records - validated via
│       │                             System.ComponentModel.DataAnnotations attributes
│       │                             and, where needed, IValidatableObject - see
│       │                             Section 5.1, not a separate Validator class)
│       └── <CommandName>Handler.cs  (FetchForWriting<T> + AppendOne, per Section 4 - not LoadAsync/Store)
├── ReadModels/
│   └── <ReadModelName>/
│       ├── <ReadModelName>.cs       (response record)
│       ├── <ReadModelName>Handler.cs   (the query - LoadAsync/Query call site unchanged even
│       │                                though it now reads a projection, not a raw document)
│       └── <TriggerEvent>ProjectorHandler.cs  (only needed for a read model distinct from an
│                                                entity's own current state - see Section 5.2)
├── Automations/
│   └── <AutomationName>/
│       └── <AutomationName>Handler.cs
└── <Module>Module.cs                (registers event streams + Inline snapshot projections per
                                       entity, replacing options.Schema.For<T>() document registration)
```
This replaced the flatter `Features/` folder from the first pass of the scaffold. `K9Crush.Modules.Identity.Api`, `K9Crush.Modules.ShelterAdoption.Api`, `K9Crush.Modules.Notifications.Api`, `K9Crush.Modules.Media.Api`, and `K9Crush.Modules.Admin.Api` are organized this way — the 5 modules live today, per ADR-031's phased retrofit (Media → Admin → Notifications → Identity → ShelterAdoption).

### 5.1 Request Validation: DataAnnotations, Not FluentValidation
Originally this scaffold used FluentValidation (`<CommandName>Validator.cs`, an `AbstractValidator<T>`), following the pattern documented in the HLD. **That never actually worked**: `WolverineFx.FluentValidation` only wires validators into Wolverine's message-bus pipeline (`IMessageBus.InvokeAsync`/`SendAsync`) - `[WolverinePost]`/`[WolverineGet]` HTTP endpoints bypass that pipeline entirely, compiling straight to ASP.NET Core delegates via Wolverine.Http's own `HttpChain` codegen. Confirmed live (2026-07-19): an invalid request reached the handler directly and 500'd on whatever guard clause failed first, instead of 400ing.

**Current, working approach**: put `[Required]`, `[MaxLength]`, `[Range]`, etc. directly on the request record's positional parameters (`[property: Required] string Name`). For anything attributes can't express (a `Guid` that must not be `Guid.Empty`, cross-field rules), have the record implement `IValidatableObject` and yield `ValidationResult`s from `Validate(ValidationContext)` - see `RequestShelterAccountRequest` for a worked example of both. Wired once, centrally, via `app.MapWolverineEndpoints(opts => opts.UseDataAnnotationsValidationProblemDetailMiddleware())` in `Api.Host/Program.cs` - no per-slice wiring needed beyond the attributes/interface on the request record itself.

### 5.2 Plain Message Handlers Must Be Named `*Handler` - Even Projectors
Wolverine's default convention-based handler discovery (`opts.Discovery.IncludeAssembly(...)`) only recognizes a `Handle` method if its containing **class name ends in `Handler`**. This bit the original `DogProfileCreatedProjector` class (file named after the trigger event, per this section's own older guidance): every other handler in the codebase already followed `*Handler` naming and worked; that one didn't, and its `Handle(DogProfileCreatedV1, IDocumentSession)` was silently never registered - confirmed live via Wolverine's own `UnknownMessageBehavior` logging (`No known handler for DogProfileCreatedV1...`) and via `wolverine_incoming_envelopes` showing the message marked `Handled` (meaning "no handler found, discarded" - not "processed by your code") with zero rows ever landing in `DiscoveryFeedItem`.

**Fix applied**: keep the file named after the trigger event (`<TriggerEvent>Projector.cs`, per Section 5's diagram) but the class inside it must be named `<TriggerEvent>ProjectorHandler` (or similar, ending in `Handler`) - satisfies both this doc's file-naming convention and Wolverine's runtime discovery requirement. Every future projector needs this, not just Discovery's.

This class also had a second, independent bug worth flagging for every future projector: its `Handle` method never called `session.SaveChangesAsync()` - `IDocumentSession.Store(...)` only stages a change, it does not auto-flush just because a handler declares `IDocumentSession` as a parameter. Every command/automation handler in this codebase calls `SaveChangesAsync` explicitly; a projector reacting to a cross-module integration event needs to as well.

**Updated scope, post-ADR-031**: this `<TriggerEvent>ProjectorHandler` convention is retired for same-module, single-stream "current state" read models — those are now served by registering the entity's own self-aggregating class as an `Inline` snapshot (Section 4), with no separate Wolverine handler at all. It's **retained unchanged** for genuinely cross-module projections, e.g. Admin's `FeedbackInboxItem` built off Identity's `FeedbackSubmittedV1` delivered over RabbitMQ — Marten's projection machinery operates against its own store's events only, not across a module boundary, so a cross-module read model still needs an explicit handler storing a plain document.

## 6. Architecture Fitness Test Additions
`K9Crush.ArchitectureTests` now has three real NetArchTest/reflection-based fitness tests (`ModuleBoundaryTests`, `EntitySerializationFitnessTests`, `HandlerNamingFitnessTests`) — the rules below extend that suite, some already built, some still aspirational:
- No type under `Commands/**` may reference another module's `Contracts` event type as something it *branches on* — commands may only produce/cascade events, never consume them. *(Aspirational — needs call-site/semantic analysis NetArchTest's declarative API can't do; revisit if a violation actually happens, per `TestingApproach.md`'s own stated policy on this class of rule.)*
- Every folder under `Automations/**` must contain a handler whose only public method takes a domain or integration event as its first parameter (never an HTTP request DTO) — this is what would have caught the original `SwipeOnDog` violation automatically. *(Aspirational, same reason as above.)*
- **`CommandStateFitnessTests.cs` (ADR-031, new):** a Mono.Cecil IL-scan (NetArchTest's own dependency) looking for actual `LoadAsync`/`Query` call instructions targeting an `Inline`-snapshot-registered type from within `Commands/**`/`Automations/**`. This supersedes the earlier plan for this rule (a plain NetArchTest type-dependency check) — that approach stops working once the write-side aggregate and the read-side snapshot are commonly the *same class* (ADR-031's dual-use pattern), since both the legitimate `FetchForWriting<T>` path and the illegitimate `LoadAsync<T>` path reference an identical type, which a declarative dependency-graph check can't distinguish. This is what would have caught the original `MatchAggregate` violation (ADR-019) automatically, and is the mechanism actually built to keep catching its ADR-031-era equivalent.
- No `[CommandName]State` type may be referenced from more than one command/automation handler — enforces "never shared" mechanically rather than by convention alone. *(Aspirational.)*
- Every class deriving from `Entity` with a non-public constructor must have `[JsonConstructor]` on that constructor, and every non-publicly-settable property on it must have `[JsonInclude]` — already built (`EntitySerializationFitnessTests.cs`). See Section 6.1 below, updated for ADR-031.

### 6.1 Entities Must Be Explicitly Marked for Serialization
An entity written with a private constructor and private property setters — a reasonable DDD instinct (only its own factory/domain methods can produce a valid instance) — directly conflicts with Marten's default serializer. `System.Text.Json`'s reflection-based converter only uses **public** constructors and only populates **public** settable members by default. This bit `DogProfile` originally (a module deleted in the 2026-07-24 descope, kept here as the historical example): the write path worked fine — serializing *out* to JSON doesn't care about constructor/setter accessibility — but `LoadAsync<DogProfile>(id)` threw `NotSupportedException` on the first real GET request.

**The fix, and the pattern every entity in the codebase must follow — under ADR-031 this now applies universally, not just to "document-classified" modules:**
```csharp
public class Application : Entity
{
    [JsonInclude] public string RejectionReason { get; private set; } = default!;
    // ... every non-public-setter property needs [JsonInclude]

    [JsonConstructor]
    private Application() { }

    public static Application Create(ApplicationSubmittedV1 e) { ... }
    public void Apply(ApplicationRejectedV1 e) { ... }
}
```
This preserves genuine encapsulation from every other caller — only the serializer gets the exception, via these two specific attributes, not a blanket "make everything public" concession. `Entity.Id` itself needed the same fix (`protected set`, now `[JsonInclude]`) since every entity inherits it. This requirement doesn't change under ADR-031's retrofit — a self-aggregating class registered as an `Inline` snapshot is still a Marten document under the hood, subject to the exact same `System.Text.Json` serialization rules a plain document entity always was.

## 7. If You're Also Modeling This on a Board
If you're tracking this on an Event Modeling board tool (timeline with COMMAND/READMODEL/AUTOMATION columns), the table in Section 3 is already in the right shape to walk column-by-column and mark each as a slice — one command, one read model, or one automation per column, named exactly as listed. That's a separate, tool-specific step from this document; ping me with the board/timeline details if you want help driving that workflow once the timeline exists there.
