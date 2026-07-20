# Event Modeling Blueprint — K9Crush Platform

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

**State-change (command):**
```csharp
[WolverinePost("/api/v1/discovery/swipe")]
public static async Task<SwipeOnDogResponse> Handle(SwipeOnDogRequest request, IDocumentSession session, ...)
{
    session.Events.Append(streamId, new DogLiked(...));
    await session.SaveChangesAsync(ct);
    return new SwipeOnDogResponse(Acknowledged: true);
}
```
One command in, event(s) appended, done. No cascaded integration events unless the event *is* the direct, unconditional consequence of the command (e.g. `CreateDogProfile` → `DogProfileCreatedV1` — that's not a hidden decision, it's the command's own result).

**State-view (read model):**
```csharp
[WolverineGet("/api/v1/profiles/dogs/{dogProfileId:guid}")]
public static async Task<Results<Ok<DogProfileResponse>, NotFound>> Handle(Guid dogProfileId, IQuerySession session, ...)
```
Plus, where the read model is a projection rather than a raw document (like `DiscoveryFeedItem`), a separate handler builds it from the triggering event(s) — `DogProfileCreatedProjector` is that handler for the discovery feed. The query handler and the projector live in the same slice folder because they're two halves of one state-view.

**Automation:**
```csharp
public static class DetectMutualMatchHandler
{
    public static async Task<MatchCreatedV1?> Handle(DogLiked domainEvent, IDocumentSession session, ...)
    {
        var state = await session.Events.AggregateStreamAsync<DetectMutualMatchState>(streamId, ...);
        // decide, then act
    }
}
```
Triggered by Marten forwarding the domain event to Wolverine (`AddMarten().IntegrateWithWolverine(m => m.SubscribeToEvent<DogLiked>())` in `Api.Host/Program.cs`). No HTTP route — this handler is never called directly by a client.

**Command state, added rule (ADR-019, see Solution Architecture doc Section 2.2 for the full writeup):** `DetectMutualMatchState` above is loaded live via `AggregateStreamAsync<T>`, not `LoadAsync<T>` against a persisted snapshot. This matters for every command or automation that needs to check event history before deciding, not just this one example. The original scaffold got this wrong — a bundled `MatchAggregate` type was persisted as a Marten snapshot and loaded wholesale, which is a DDD-aggregate-shaped read model doing command-validation duty. The fix: **every command/automation that needs stream history gets its own minimal, never-shared, never-persisted `[CommandName]State` type**, named after that command and containing only the fields its one decision needs. Two commands needing "similar-looking" state still get two separate types — resist the urge to consolidate them, since that's exactly how the bundle creeps back in.

## 5. Folder Convention (now enforced in the scaffold)
```
K9Crush.Modules.<Module>.Api/
├── Commands/
│   └── <CommandName>/
│       ├── <CommandName>.cs         (request + response records - validated via
│       │                             System.ComponentModel.DataAnnotations attributes
│       │                             and, where needed, IValidatableObject - see
│       │                             Section 5.1, not a separate Validator class)
│       └── <CommandName>Handler.cs
├── ReadModels/
│   └── <ReadModelName>/
│       ├── <ReadModelName>.cs       (response record)
│       ├── <ReadModelName>Handler.cs   (the query)
│       └── <TriggerEvent>ProjectorHandler.cs  (keeps the read model current, if not a raw document - see Section 5.2 for why the class must end in "Handler" even though the file is named after the trigger event)
├── Automations/
│   └── <AutomationName>/
│       └── <AutomationName>Handler.cs
└── <Module>Module.cs
```
This replaced the flatter `Features/` folder from the first pass of the scaffold. `K9Crush.Modules.Profiles.Api`, `K9Crush.Modules.Discovery.Api`, `K9Crush.Modules.Identity.Api`, and `K9Crush.Modules.ShelterAdoption.Api` are organized this way.

### 5.1 Request Validation: DataAnnotations, Not FluentValidation
Originally this scaffold used FluentValidation (`<CommandName>Validator.cs`, an `AbstractValidator<T>`), following the pattern documented in the HLD. **That never actually worked**: `WolverineFx.FluentValidation` only wires validators into Wolverine's message-bus pipeline (`IMessageBus.InvokeAsync`/`SendAsync`) - `[WolverinePost]`/`[WolverineGet]` HTTP endpoints bypass that pipeline entirely, compiling straight to ASP.NET Core delegates via Wolverine.Http's own `HttpChain` codegen. Confirmed live (2026-07-19): an invalid request reached the handler directly and 500'd on whatever guard clause failed first, instead of 400ing.

**Current, working approach**: put `[Required]`, `[MaxLength]`, `[Range]`, etc. directly on the request record's positional parameters (`[property: Required] string Name`). For anything attributes can't express (a `Guid` that must not be `Guid.Empty`, cross-field rules), have the record implement `IValidatableObject` and yield `ValidationResult`s from `Validate(ValidationContext)` - see `RequestShelterAccountRequest` for a worked example of both. Wired once, centrally, via `app.MapWolverineEndpoints(opts => opts.UseDataAnnotationsValidationProblemDetailMiddleware())` in `Api.Host/Program.cs` - no per-slice wiring needed beyond the attributes/interface on the request record itself.

### 5.2 Plain Message Handlers Must Be Named `*Handler` - Even Projectors
Wolverine's default convention-based handler discovery (`opts.Discovery.IncludeAssembly(...)`) only recognizes a `Handle` method if its containing **class name ends in `Handler`**. This bit the original `DogProfileCreatedProjector` class (file named after the trigger event, per this section's own older guidance): every other handler in the codebase already followed `*Handler` naming and worked; that one didn't, and its `Handle(DogProfileCreatedV1, IDocumentSession)` was silently never registered - confirmed live via Wolverine's own `UnknownMessageBehavior` logging (`No known handler for DogProfileCreatedV1...`) and via `wolverine_incoming_envelopes` showing the message marked `Handled` (meaning "no handler found, discarded" - not "processed by your code") with zero rows ever landing in `DiscoveryFeedItem`.

**Fix applied**: keep the file named after the trigger event (`<TriggerEvent>Projector.cs`, per Section 5's diagram) but the class inside it must be named `<TriggerEvent>ProjectorHandler` (or similar, ending in `Handler`) - satisfies both this doc's file-naming convention and Wolverine's runtime discovery requirement. Every future projector needs this, not just Discovery's.

This class also had a second, independent bug worth flagging for every future projector: its `Handle` method never called `session.SaveChangesAsync()` - `IDocumentSession.Store(...)` only stages a change, it does not auto-flush just because a handler declares `IDocumentSession` as a parameter. Every command/automation handler in this codebase calls `SaveChangesAsync` explicitly; a projector reacting to a cross-module integration event needs to as well.

## 6. Architecture Fitness Test Additions
Once `K9Crush.ArchitectureTests` is built out (still pending), add rules enforcing this discipline mechanically rather than relying on code review alone:
- No type under `Commands/**` may reference another module's `Contracts` event type as something it *branches on* — commands may only produce/cascade events, never consume them.
- No type under `Commands/**` may call `session.Events.Append` more than once for *different* stream concerns in one handler (a rough proxy for "one decision").
- Every folder under `Automations/**` must contain a handler whose only public method takes a domain or integration event as its first parameter (never an HTTP request DTO) — this is what would have caught the original `SwipeOnDog` violation automatically.
- No type deriving from `Marten.Events.Projections`/registered via `Projections.Snapshot<T>()` may be loaded (`LoadAsync<T>`) inside a type under `Commands/**` or `Automations/**` — command/automation state must come from `AggregateStreamAsync<T>`, never a persisted snapshot. This is what would have caught the original `MatchAggregate` violation (ADR-019) automatically.
- No `[CommandName]State` type may be referenced from more than one command/automation handler — enforces "never shared" mechanically rather than by convention alone.
- **New:** every class deriving from `Entity` with a non-public constructor must have `[JsonConstructor]` on that constructor, and every non-publicly-settable property on it must have `[JsonInclude]`. This is what would have caught the `DogProfile` deserialization bug (Section 6.1 below) at build/test time instead of a 500 on the first real GET request.

### 6.1 Document Entities Must Be Explicitly Marked for Serialization
`DogProfile` was originally written with a private constructor and private property setters — a reasonable DDD instinct (only `DogProfile.Create(...)` and its own domain methods can produce a valid instance) that directly conflicts with Marten's default serializer. `System.Text.Json`'s reflection-based converter only uses **public** constructors and only populates **public** settable members by default. The write path (`session.Store(dogProfile)`) worked fine — serializing *out* to JSON doesn't care about constructor/setter accessibility. The read path (`session.LoadAsync<DogProfile>(id)`) is what broke, with a `NotSupportedException` on the very first GET request that actually exercised it.

**The fix, and the pattern every future document-style entity must follow** (Identity, Subscriptions, Moderation, Places, Shelter & Adoption — anything classified as "document" rather than "event-sourced" in the Solution Architecture doc's Section 2.1 table):
```csharp
public class DogProfile : Entity
{
    [JsonInclude] public string Name { get; private set; } = default!;
    // ... every non-public-setter property needs [JsonInclude]

    [JsonConstructor]
    private DogProfile() { }

    public static DogProfile Create(...) { ... }
}
```
This preserves genuine encapsulation from every other caller — only the serializer gets the exception, via these two specific attributes, not a blanket "make everything public" concession. `Entity.Id` itself needed the same fix (`protected set`, now `[JsonInclude]`) since every document type inherits it.

**What did *not* need this fix:** `DiscoveryFeedItem` (Discovery's read-model projection) uses plain public settable properties and an implicit public constructor — already serialization-safe, no annotations needed. The pattern only bites types that deliberately restrict their own constructor/setters, which is exactly the document-style entities this note is about.

## 7. If You're Also Modeling This on a Board
If you're tracking this on an Event Modeling board tool (timeline with COMMAND/READMODEL/AUTOMATION columns), the table in Section 3 is already in the right shape to walk column-by-column and mark each as a slice — one command, one read model, or one automation per column, named exactly as listed. That's a separate, tool-specific step from this document; ping me with the board/timeline details if you want help driving that workflow once the timeline exists there.
