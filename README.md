# K9Crush

A dog-dating/adoption platform: swipe-to-match between dog owners, plus
shelter-run adoption listings and applications. Built with an
[event-modeling](https://eventmodeling.org/) discipline (state-change /
state-view / automation slices) end to end, from design docs through
codegen to the running .NET backend.

## Where things live

- **`code/K9Crush-scaffold/K9Crush/`** — the actual product: a .NET modular
  monolith (vertical slice architecture, Marten + Wolverine on
  PostgreSQL/RabbitMQ, Redis) with a Blazor Web App frontend, YARP gateway,
  and Supabase Cloud for identity/Postgres/storage. Start with its own
  `README.md` and `GETTING_STARTED.md` to build and run it, and
  `docs/05-event-modeling-blueprint.md` before adding a new slice.

- **Design docs** — `HLD/`, `Solution Arch/`, `Project Plan/`, `Infra/`,
  `Spec/` (the board's full `K9CRUSH.emlang.yaml` export), and
  `eventmodeling/` (the blueprint doc plus `Features/features.md`). These
  are the source of truth the slices are built from — the codegen skills
  (below) are told to invent nothing that isn't in them.

## Slice codegen tooling

Slices are designed on an [eventmodelers.ai](https://eventmodelers.ai)
board, then turned into code by Claude Code using:

- **`.claude/skills/`** — the actual codegen skills (`connect`,
  `load-slice`, `update-slice-status`, `learn-eventmodelers-api`,
  `build-state-change`, `build-state-view`, `build-automation`). Live at
  the repo root so any Claude Code session anywhere in the repo can use
  them.
- **`build-kit-dotnet/`** — the supporting Ralph loop + board-sync tooling
  that drives those skills against `code/K9Crush-scaffold/K9Crush/`. See
  its own `README.md` for how to run it.
- **`build-kit/`** — the original Node/Express scaffold that
  eventmodelers.ai's code-export tool generates, kept only as an
  unmodified reference (K9Crush isn't built on this stack). See its own
  `README.md`.
- **`.eventmodelers/config.json`** — board credentials (id, token, org,
  base URL), gitignored; set up interactively via the `connect` skill if
  missing.

## Workflow notes

- This repo pushes `dev` only — `main` is synced deliberately, not as a
  side effect of finishing a slice.
