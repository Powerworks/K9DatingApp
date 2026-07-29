# build-kit-dotnet-es

Turns eventmodelers.ai board slices into working code for a Wolverine.Http +
Marten + RabbitMQ .NET solution, using Marten's **event-sourcing mode
exclusively** — no document-store branch, no per-module storage decision.

**Status: untested.** This is a generic template forked from
`build-kit-dotnet` (this repo's own K9Crush-specific kit) after that
project's ADR-031 retrofit proved out "event-sourced everywhere, with
selective Inline snapshots" as the right default. It has not yet been run
against a real board or a real .NET solution — treat the Node tooling and
skill instructions as a first draft to validate on the next project that
uses it, not as something already exercised end-to-end.

## Why a separate kit instead of editing build-kit-dotnet in place

`build-kit-dotnet`'s code-gen skills (at this repo's root `.claude/skills/`)
encode a **per-module** choice between Marten-as-document-store and
Marten-as-event-store, because that project hadn't settled the question
when those skills were written. It has settled it since — every module was
retrofitted to event sourcing (see this repo's git history: "retrofit
\<Module\> to event sourcing (ADR-031 Phase N/5)"). Rather than edit that
project's own skills to remove a decision framework that's specific to how
*that* project got there, this kit exists to be a clean starting point for
any *new* project that already knows it wants event sourcing from slice
one — no per-slice "check the module's config to see which pattern this
one uses" step, because there's only one pattern.

## What's different from build-kit-dotnet

- **`build-state-change`**: Step 2 (the document-vs-event-sourced fork) is
  gone. Every entity is a self-aggregating `Create`/`Apply` event stream.
  Folds in the lessons from K9Crush's retrofit as first-class guidance
  rather than after-the-fact fixes: `[JsonInclude]`/`[JsonConstructor]` is
  called out up front (not discovered via a `NotSupportedException` on the
  first real read), the Inline-snapshot decision is its own explicit step
  (add one only when a read model queries the entity by id — never by
  default), and the two concurrency exception types
  (`JasperFx.ConcurrencyException` vs.
  `Marten.Exceptions.ConcurrentUpdateException`) are documented together
  since a real race condition is what it took to find both.
- **`build-state-view`**: clarifies explicitly that "event-sourced only"
  describes the *write side* — read-model documents (whether projector-built
  or an entity's own Inline snapshot read back directly) are still plain
  Marten documents. This isn't a carve-out from the event-sourcing rule;
  it's what the rule was always about.
- **`build-automation`**: the document-store branch (loading a plain
  document instead of computing decision state) is gone. Also makes
  explicit that an automation's own decision state
  (`AggregateStreamAsync`-computed, single-purpose, never persisted, never
  shared across handlers) is a *different* rule from an entity's Inline
  snapshot (durable, shared, meant to be queried) — the original
  `build-kit-dotnet` skills stated the "never a shared persisted snapshot"
  rule in a way that, read after ADR-031 landed, looked like it contradicted
  Inline snapshots outright. It doesn't — they're answering two different
  questions — but the original phrasing didn't make that clear, so this
  version says so directly.
- **`connect` / `load-slice` / `update-slice-status` / `learn-eventmodelers-api`**:
  unchanged in substance, just re-pointed at this kit's own paths
  (`build-kit-dotnet-es/...` instead of `build-kit-dotnet/...`) and
  genericized (`<SolutionName>`, `<path-to-your-.NET-solution>` placeholders
  instead of `K9Crush`/`code/K9Crush-scaffold/K9Crush`).
- **Node tooling** (`ralph-claude.js`, `ralph-ollama.js`, `ralph.sh`,
  `realtime-agent.js`, `code-export.mjs`, `lib/*`): copied verbatim except
  for the `project_dir` default. `build-kit-dotnet` hardcoded
  `code/K9Crush-scaffold/K9Crush` as its default; this kit has no fixed
  project to default to, so `project_dir` is now **required** — pass it as
  an argument or set `DOTNET_PROJECT_DIR`.

## Layout

```
build-kit-dotnet-es/
├── package.json            (deps: @supabase/supabase-js — run `npm install` once)
├── ralph-claude.js          entry point: Ralph loop + realtime agent, Claude Code as executor
├── ralph-ollama.js          entry point: same loop, local Ollama model as executor
├── ralph.sh                 bash-only alternative loop
├── realtime-agent.js        standalone realtime agent (separate-terminal use)
├── code-export.mjs          local bridge server for the eventmodelers.ai web UI (port 3001 by default)
├── orchestrate.mjs          picks a board chapter, retrofits SLICE_BORDER markers onto its
│                            columns if missing, flips them Planned, then spawns N Ralph
│                            instances in parallel git worktrees and watches them to completion
├── lib/
│   ├── ralph.js             shared runtime: config resolution, realtime subscription, task queue, the loop itself
│   ├── ollama-agent.js       Ollama executor, called by ralph-ollama.js
│   ├── agent.sh              thin wrapper around the `claude` CLI, called by ralph.sh
│   ├── prompt.md             Phase 1 prompt (load a slice from the board)
│   └── backend-prompt.md     Phase 2 prompt (build a Planned slice)
├── .claude/
│   └── skills/
│       ├── connect/SKILL.md
│       ├── load-slice/SKILL.md
│       ├── update-slice-status/SKILL.md
│       ├── learn-eventmodelers-api/SKILL.md
│       ├── build-state-change/SKILL.md   ← event-sourced only
│       ├── build-state-view/SKILL.md     ← event-sourced only
│       └── build-automation/SKILL.md     ← event-sourced only
├── hooks/
│   ├── quality-gate.sh      Pre-checkin quality gate (Phase 1 of quality-checks.md) — a
│   │                        PreToolUse hook script; installs into the TARGET solution's
│   │                        own .claude/settings.json, not this kit's — see hooks/README.md
│   └── README.md            what it checks, how to install it, known Phase 1 limitations
├── .eventmodelers/          (gitignored — board credentials, see `connect`)
├── .slices/                 (gitignored — board slice cache, written by load-slice / Ralph)
├── tasks.json               (gitignored — Ralph's task queue)
├── progress.txt             (gitignored — Ralph's progress log)
├── slice-timings.jsonl      (gitignored — per-slice InProgress→terminal wall-clock history, written by orchestrate.mjs)
├── ralph-N.log              (gitignored — orchestrate.mjs's per-instance Ralph output)
├── quality-checks.md        (tracked — the quality/guardrails plan; Phase 1 implemented, see hooks/)
└── AGENT.md                  (tracked — accumulated cross-session learnings, event-sourcing-only lessons pre-seeded)
```

Self-contained on purpose: unlike `build-kit-dotnet` (a sibling of its
target project, code-gen skills living separately at the repo root because
that layout was already established), this kit carries its own
`.claude/skills/` so the whole thing can be dropped into a fresh repo as a
single unit and be immediately discoverable by Claude Code.

## Setting up in a new project

1. Copy this whole `build-kit-dotnet-es/` directory into the target repo
   (as a sibling of the .NET solution, or wherever suits that repo's
   layout — nothing here assumes a specific position other than "somewhere
   inside the repo, so `connect`'s ancestor-walk for `.eventmodelers/config.json`
   can find it").
2. Find-and-replace the placeholders used throughout the skills and
   `AGENT.md`:
   - `<SolutionName>` — your solution's root namespace (e.g. what
     `K9Crush` was for the source project — `<SolutionName>.Modules.<Context>.Api`,
     `<SolutionName>.Api.Host`, etc.)
   - `<path-to-your-.NET-solution>` — the relative path from repo root to
     your `.sln` file's directory
3. `npm install` (once, for `@supabase/supabase-js`).
4. Run the `connect` skill (or just start using any other skill — they all
   invoke `connect` first) to set up `.eventmodelers/config.json`.
5. Confirm your project's `Program.cs` has the event-sourcing wiring the
   skills assume: `AddMarten(...).IntegrateWithWolverine()`, module-owned
   `IMartenModuleConfiguration.Configure(StoreOptions)` implementations
   setting `options.Events.DatabaseSchemaName`, and a central exception
   handler mapping `JasperFx.ConcurrencyException`/
   `Marten.Exceptions.ConcurrentUpdateException` to `409 Conflict`. None of
   the skills set this up for you — they assume it's already there, the
   same way the source project's skills did.
6. Install the pre-checkin quality gate: create
   `<target-solution-root>/.claude/settings.json` registering
   `hooks/quality-gate.sh` as a `PreToolUse` hook on the `Bash` matcher,
   and add `.claude/state/` to the target solution's own `.gitignore` —
   see `hooks/README.md` for the exact config and why it has to live in
   the *target solution's* `.claude/` folder, not this kit's.
7. Start building slices — `build-state-change` for commands,
   `build-state-view` for read models, `build-automation` for event-triggered
   reactions. Once several slices exist as `Planned` on a chapter, use
   `orchestrate.mjs` (below) to build a whole chapter unattended instead of
   running one slice at a time through the skills manually.

## Running

```bash
npm install   # once, for @supabase/supabase-js

# Ralph — the autonomous loop + realtime board subscription. project_dir
# is required (no default — see "What's different" above).
node ralph-claude.js /path/to/your/solution
DOTNET_PROJECT_DIR=/path/to/your/solution node ralph-claude.js

# Local Ollama model instead of Claude Code
OLLAMA_MODEL=qwen3:8b node ralph-ollama.js /path/to/your/solution   # run `ollama serve` first

# Bash-only alternative to the JS entry points
./ralph.sh [iterations] /path/to/your/solution

# CodeExport — local bridge server for the eventmodelers.ai web UI
node code-export.mjs
PORT=3002 WORKSPACE_PATH=/path/to/repo node code-export.mjs
```

### Orchestrating a whole chapter

Instead of running Ralph once and leaving it to poll one context at a time,
`orchestrate.mjs` retrofits missing slices onto a chapter, flips everything
in it to `Planned`, and starts N Ralph instances against it in parallel —
each in its own git worktree, so concurrent instances never race each
other's file writes in a shared working tree (a real, previously-confirmed
failure mode — see `AGENT.md`'s "Concurrent Ralph agents" entries for what
happens without this).

```bash
# Interactive — lists chapters on the board, prompts for a number
node orchestrate.mjs /path/to/your/solution

# Non-interactive — build a named chapter with 3 parallel instances,
# give up watching (not stop) after 90 minutes if it's not done by then
node orchestrate.mjs /path/to/your/solution "Shelter Reviews Application" --parallel 3 --timeout-minutes 90

# Resume watching a chapter you already started in another terminal
node orchestrate.mjs /path/to/your/solution "Shelter Reviews Application" --watch

# After killing instances early (or a --watch timeout), merge+clean up
# whatever worktree branches exist without needing the chapter name again
node orchestrate.mjs /path/to/your/solution --merge --parallel 3
```

Each instance's git worktree lives as a sibling directory
(`<solution-dir>-ralph-1`, `-ralph-2`, ...) on branch `ralph/instance-N`,
off whatever branch you were on when you ran the command. Once every
tracked slice in the chapter reaches `Done` or `Blocked`, each instance's
branch is merged back automatically and its worktree removed. Per-instance
output goes to `ralph-1.log`/`ralph-2.log`/... in this directory (not the
terminal) — `orchestrate.mjs` itself only prints its own retrofit/flip/
watch progress. Per-slice `InProgress`→terminal timing is appended to
`slice-timings.jsonl` as it happens, plus a running average printed at the
end of each watch.

Two `.eventmodelers/config.json` fields (optional, read by `lib/ralph.js`,
not by `orchestrate.mjs` itself) matter more once you're running several
instances unattended:
- `maxSlicesPerRun` — a Ralph instance exits cleanly after building this
  many slices, instead of polling forever. Useful for bounding a single
  `--parallel` run's blast radius.
- `maxBudgetUsdPerSlice` — passed to `claude -p` as `--max-budget-usd`, a
  per-slice spend cap (`ralph-claude.js` only).

Both are absent by default (no cap) — set them in `.eventmodelers/config.json`
alongside `token`/`boardId`/etc. if you want them.

## Config

Credentials (board id, token, org id, base URL) come from
`<repo-root>/.eventmodelers/config.json` — shared with the `connect` skill,
gitignored. `lib/ralph.js`'s config loader walks up from
`build-kit-dotnet-es/` through ancestor directories to find it. Note:
`ralph.sh` only checks `build-kit-dotnet-es/.eventmodelers/config.json`
directly, not ancestors — use `ralph-claude.js` if you rely on a repo-root
copy without one directly here.
