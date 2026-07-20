# build-kit-dotnet

Turns eventmodelers.ai board slices into working code for the K9Crush
solution (`code/K9Crush-scaffold/K9Crush/`), using Wolverine.Http + Marten +
RabbitMQ instead of emmett/Express/Knex.

The code-gen skills themselves live at the **repo root**, in
`.claude/skills/` (`connect`, `load-slice`, `update-slice-status`,
`learn-eventmodelers-api`, `build-state-change`, `build-state-view`,
`build-automation`) — not under this folder — so they're discoverable by
any Claude Code session anywhere in this repo. This folder holds the two
supporting tools plus their shared runtime state — both stay in
**JavaScript/Node**, same as the original `build-kit/` tool; only the
skills themselves target the .NET stack.

## Layout

```
build-kit-dotnet/
├── package.json            (deps: @supabase/supabase-js — run `npm install` once)
├── ralph-claude.js          entry point: Ralph loop + realtime agent, Claude Code as executor
├── ralph-ollama.js          entry point: same loop, local Ollama model as executor
├── ralph.sh                 bash-only alternative loop (see note in the file)
├── realtime-agent.js        standalone realtime agent (only needed to run it in a separate terminal)
├── code-export.mjs          local bridge server for the eventmodelers.ai web UI (port 3001 by default)
├── lib/
│   ├── ralph.js             shared runtime: config resolution, realtime subscription, task queue, the loop itself
│   ├── ollama-agent.js       Ollama executor, called by ralph-ollama.js
│   ├── agent.sh              thin wrapper around the `claude` CLI, called by ralph.sh
│   ├── prompt.md             Phase 1 prompt (load a slice from the board)
│   └── backend-prompt.md     Phase 2 prompt (build a Planned slice)
├── .eventmodelers -> not here; see repo root .eventmodelers/config.json
├── .slices/                 (gitignored — board slice cache, written by load-slice / Ralph)
├── tasks.json               (gitignored — Ralph's task queue)
├── progress.txt             (gitignored — Ralph's progress log)
└── AGENT.md                  (tracked — accumulated cross-session learnings)
```

This is a straight adaptation of `build-kit/.build-kit/` — same files, same
logic, just relocated (a sibling of the target project under the repo root,
rather than nested one level inside it) and re-pointed at the K9Crush
project and the repo-root `.claude/skills/` instead of the original
Node/emmett reference app.

## Running

```bash
npm install   # once, for @supabase/supabase-js

# Ralph — the autonomous loop + realtime board subscription. Defaults
# project_dir to code/K9Crush-scaffold/K9Crush; pass a path to override.
node ralph-claude.js
node ralph-claude.js /path/to/other/project

# Local Ollama model instead of Claude Code
OLLAMA_MODEL=qwen3:8b node ralph-ollama.js   # run `ollama serve` first

# Bash-only alternative to the JS entry points
./ralph.sh [iterations] [project_dir]

# CodeExport — local bridge server for the eventmodelers.ai web UI
node code-export.mjs
PORT=3002 WORKSPACE_PATH=/path/to/repo node code-export.mjs
```

## Config

Credentials (board id, token, org id, base URL) come from
`<repo-root>/.eventmodelers/config.json` — shared with the `connect` skill,
gitignored. `lib/ralph.js`'s config loader walks up from `build-kit-dotnet/`
through ancestor directories to find it (see `.claude/skills/connect/SKILL.md`
for the resolution order and how to set it up interactively if it's ever
missing). Note: `ralph.sh` only checks `build-kit-dotnet/.eventmodelers/config.json`
directly, not ancestors — use `ralph-claude.js` if you rely on the
repo-root copy without one directly here.

## Adjustments made vs. the original `build-kit/`

- `ralph-claude.js`/`ralph-ollama.js`/`ralph.sh`'s `project_dir` default
  changed from "parent of the kit dir" to `code/K9Crush-scaffold/K9Crush`
  explicitly — `build-kit-dotnet/` is a sibling of the target project here,
  not its parent, unlike the original nested `.build-kit/` layout.
- `code-export.mjs`: the original hardcoded `.slices` as the git
  pathspec/add-target for `/api/slices` and `/api/git`, which only works if
  `SLICES_DIR` sits directly under `repoPath` — it doesn't here (or in the
  original's own nested layout, for that matter). Fixed to compute the real
  relative path instead (`SLICES_PATHSPEC`).
- `lib/ralph.js` and `lib/agent.sh` are otherwise **unmodified** — they're
  already fully generic (parameterized by `kitDir`/`projectDir`), no
  app-specific paths to update.
- `package.json`: `name` updated, and its `start` script pointed at
  `ralph-claude.js` (the original referenced a `ralph.js` that doesn't
  exist at the top level in either layout).
