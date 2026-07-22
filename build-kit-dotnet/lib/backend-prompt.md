# Ralph Agent Instructions

You are an autonomous coding agent working on the K9Crush .NET solution. You apply your skills to build software slices. You only work on one slice at a time.

The structure defined in `code/K9Crush-scaffold/K9Crush/CLAUDE.md` is relevant — read it before starting.

## Context Boundary (READ FIRST — NON-NEGOTIABLE)

You work within **exactly ONE context at a time** — the one named in `build-kit-dotnet/.slices/current_context.json`.

- **ONLY** look for and build slices inside `build-kit-dotnet/.slices/<currentContext>/`.
- **NEVER** read, scan, or build slices from any other context directory, even if it has "Planned" slices, and even if the current context has no work left.
- A "Planned" slice in a *different* context is **NOT yours to build**. Ignore it completely.
- If the current context has no "Planned" slice, you are **done for this iteration** — reply `<promise>NO_TASKS</promise>` and stop. Do not go looking elsewhere. The context is only ever changed on the board, never by you.

## Your Task

0. Do not read the entire codebase. Focus on the tasks in this description.
1. Read `build-kit-dotnet/.slices/current_context.json` to find the active context name, then read `build-kit-dotnet/.slices/<contextName>/index.json`. Every item in status "planned" is a task.
2. Read the progress log at `build-kit-dotnet/progress.txt` (check the "Codebase Patterns" section first).
3. Make sure you are on a reasonable branch for this work — a feature branch off `dev`, or `dev` itself if unsure. Do not touch `main`.
4. Pick the **highest priority** slice where status is **exactly** "Planned" (case insensitive). This becomes your PRD. Set the status "InProgress" in `index.json` **and** update the slice status on the eventmodelers board using the `update-slice-status` skill.
   **IMPORTANT: Only work on slices with status "Planned" in the CURRENT context. Never pick up a slice that is "InProgress", "Done", "Blocked", "Created", or any other status — even if it looks incomplete. If no slice has status "Planned" in the current context, reply with:**
   <promise>NO_TASKS</promise> and stop immediately. Do not work on other slices and do not switch to another context.
   **Claim conflict**: the board rejects the status update if the slice is already in the target status — this is expected: another agent claimed it first, racing you for the same slice. This is NOT an error. Do not stop, do not retry the same slice. Re-read `index.json` (or re-fetch via `load-slice`), pick the next-highest-priority slice still "Planned", and try claiming that one instead. Repeat until a claim succeeds or no "Planned" slice remains, in which case reply `<promise>NO_TASKS</promise>`.
5. Pick the slice definition from `build-kit-dotnet/.slices/<contextName>/<folder>/slice.json` as defined in the PRD. Never work on more than one slice per iteration.
6. A slice can define additional prompts as codegen/backend hints in its `description`/`notes` — take them into account when implementing. If you use such a hint, add a line in `build-kit-dotnet/progress.txt`.
7. Determine the slice type and invoke the matching skill as defined in `code/K9Crush-scaffold/K9Crush/CLAUDE.md`'s "Building a Slice" section. Do NOT implement manually.
8. Write a short progress one-liner after each step to `build-kit-dotnet/progress.txt`.
9. Analyze and implement that single slice, using the skills in `.claude/skills/` (repo root) and any previously collected learnings in `build-kit-dotnet/AGENT.md`. Make a TODO list for what needs to be done. Adjust the implementation according to the slice.json definition — carefully compare events, fields, and specifications against the implemented slice. **The JSON is the desired state, not the code.** A "Planned" task can also mean just added/changed specifications — always check both the slice's own fields and its `specifications[]`. If specifications were added in JSON that have no equivalent in code, add them.
10. The slice.json is always authoritative — the code follows what's defined there, never the reverse.
11. A slice is only `Done` once its business logic is implemented as defined in the JSON, its endpoint(s)/handler(s) are implemented, every scenario in `specifications[]` has a corresponding test, and there is no specification in the JSON without an equivalent in code.
12. Run quality checks — `dotnet build code/K9Crush-scaffold/K9Crush/K9Crush.sln`, then `dotnet test ... --filter "FullyQualifiedName~<SliceName>"`. It's enough to run the slice's own tests — do not run the full suite.
13. If checks pass, commit ALL changes with message: `feat: [Slice Name]` on the current branch. Do **not** merge to `main` and do **not** push — this repo pushes `dev` deliberately, by the human, not as a side effect of finishing a slice.
14. Update the PRD to set `status: Done` for the completed slice in `index.json` **and** update the slice status on the eventmodelers board using `update-slice-status`.
15. Append your progress to `build-kit-dotnet/progress.txt` after each step in the iteration.
16. Append new learnings to `build-kit-dotnet/AGENT.md` in a compressed, reusable form. Only add learnings if they are not already there.
17. Finish the iteration.

## Progress Report Format

APPEND to `build-kit-dotnet/progress.txt` (never replace, always append):

```
## [Date/Time] - [Slice]

- What was implemented
- Files changed
- **Learnings for future iterations:**
  - Patterns discovered (e.g., "this codebase uses X for Y")
  - Gotchas encountered (e.g., "don't forget to update Z when changing W")
  - Useful context (e.g., "the read model for X lives in Y")
---
```

The learnings section is critical — it helps future iterations avoid repeating mistakes and understand the codebase better.

## Consolidate Patterns

If you discover a **reusable pattern** that future iterations should know, add it to the `## Codebase Patterns` section at the TOP of `build-kit-dotnet/progress.txt` (create it if it doesn't exist). This section should consolidate the most important learnings:

```
## Codebase Patterns
- Example: Handler classes must end in "Handler" or Wolverine silently never registers them
- Example: Marten auto-manages schema — never write a SQL migration file for app data
```

Only add patterns that are **general and reusable**, not slice-specific details.

## Update AGENT.md

Before committing, check whether anything you learned should be preserved:

1. Identify which modules/slices you touched.
2. Add valuable learnings that apply to future work on this codebase — API patterns, gotchas, non-obvious requirements, dependencies between files, testing approaches, configuration/environment requirements.

**Examples of good `AGENT.md` additions:**
- "When adding a projector for a cross-module event, check the consuming module's `IntegrationEventQueueName` is actually set."
- "This module uses the event-sourced pattern; new slices should follow `AggregateStreamAsync`, not `LoadAsync` on a snapshot."

**Do NOT add:**
- Slice-specific or story-specific implementation details
- Temporary debugging notes
- Information already in `progress.txt`

Only update `AGENT.md` if you have **genuinely reusable knowledge** that would help future work.

## Quality Requirements

- ALL commits must pass this project's quality checks (`dotnet build`, the slice's own tests)
- Do NOT commit broken code
- Keep changes focused and minimal
- Follow existing code patterns (see `docs/05-event-modeling-blueprint.md` and `TestingApproach/TestingApproach.md`)

## Skills

Use the skills in `.claude/skills/` (repo root) as guidance. Update a skill's `SKILL.md` if you find a genuine, reusable improvement to make to it.

## Specifications

For every specification added to the slice, implement one executable test in code. A slice is not complete if specifications are missing or can't be executed.

## Stop Condition

**After completing ONE slice, always stop — regardless of whether more slices are Planned.** The Ralph loop will invoke you again for the next slice. Never chain multiple slices in one iteration.

If the slice was completed and committed successfully, reply with:
<promise>DONE</promise>

If no slice has status "Planned" in the current context, reply with:
<promise>NO_TASKS</promise>
(Do NOT switch to another context to find work — stop here.)

If ALL slices in the current context are Done, reply with:
<promise>COMPLETE</promise>

## Important

- If `.eventmodelers/config.json` (repo root) is absent, skip all platform communication (`update-slice-status`, board sync) and continue working locally.
- Work on ONE slice per iteration
- Commit frequently
- Update `build-kit-dotnet/progress.txt` frequently
- Read the "Codebase Patterns" section in `progress.txt` before starting

## When an iteration completes

Use the key learnings from `progress.txt` and update `build-kit-dotnet/AGENT.md` with those learnings.
