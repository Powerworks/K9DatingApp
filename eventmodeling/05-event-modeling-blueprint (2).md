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
| Identity | ProvisionOwnerOnKeycloakSignup | A | Keycloak user-created event | `OwnerRegistered` *(bridges external IdP event into our stream — see note below)* |
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
| Moderation | RevokeAccessOnBan | A | `UserBanned` | *(no new event — calls Keycloak admin API to revoke sessions)* |
| Moderation | ModerationQueue | V | `ContentFlagged` | — |

Only Identity/Profiles/Discovery are scaffolded so far; the rest of the table is the target shape for when those modules get built, so the pattern is consistent from the start rather than retrofitted.

> **Identity note:** since Keycloak now owns registration (per your earlier decision), `RegisterOwner`/`OwnerRegistered` most likely becomes purely reactive — an automation bridging Keycloak's own user-lifecycle event into our domain — rather than a command our own UI calls directly. Worth confirming once the Identity module is actually built, since it changes whether `RegisterOwner` is a C or an A.

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
│       ├── <CommandName>.cs         (request + response records)
│       ├── <CommandName>Handler.cs
│       └── <CommandName>Validator.cs
├── ReadModels/
│   └── <ReadModelName>/
│       ├── <ReadModelName>.cs       (response record)
│       ├── <ReadModelName>Handler.cs   (the query)
│       └── <TriggerEvent>Projector.cs  (keeps the read model current, if not a raw document)
├── Automations/
│   └── <AutomationName>/
│       └── <AutomationName>Handler.cs
└── <Module>Module.cs
```
This replaced the flatter `Features/` folder from the first pass of the scaffold. `K9Crush.Modules.Profiles.Api` and `K9Crush.Modules.Discovery.Api` have already been reorganized this way.

## 6. Architecture Fitness Test Additions
Once `K9Crush.ArchitectureTests` is built out (still pending), add rules enforcing this discipline mechanically rather than relying on code review alone:
- No type under `Commands/**` may reference another module's `Contracts` event type as something it *branches on* — commands may only produce/cascade events, never consume them.
- No type under `Commands/**` may call `session.Events.Append` more than once for *different* stream concerns in one handler (a rough proxy for "one decision").
- Every folder under `Automations/**` must contain a handler whose only public method takes a domain or integration event as its first parameter (never an HTTP request DTO) — this is what would have caught the original `SwipeOnDog` violation automatically.
- No type deriving from `Marten.Events.Projections`/registered via `Projections.Snapshot<T>()` may be loaded (`LoadAsync<T>`) inside a type under `Commands/**` or `Automations/**` — command/automation state must come from `AggregateStreamAsync<T>`, never a persisted snapshot. This is what would have caught the original `MatchAggregate` violation (ADR-019) automatically.
- No `[CommandName]State` type may be referenced from more than one command/automation handler — enforces "never shared" mechanically rather than by convention alone.

## 7. If You're Also Modeling This on a Board
If you're tracking this on an Event Modeling board tool (timeline with COMMAND/READMODEL/AUTOMATION columns), the table in Section 3 is already in the right shape to walk column-by-column and mark each as a slice — one command, one read model, or one automation per column, named exactly as listed. That's a separate, tool-specific step from this document; ping me with the board/timeline details if you want help driving that workflow once the timeline exists there.
