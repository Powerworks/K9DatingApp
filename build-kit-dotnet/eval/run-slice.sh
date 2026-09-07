#!/bin/bash
# run-slice.sh — the eval harness driver (workstream 2 of the ShelterAdoption
# eval fixture; see Active_Projects/K9crush/Orca Setup Brief.md §14 in the
# Obsidian vault for the full decision).
#
# For ONE slice from eval/slices-manifest.json, this script:
#   1. Resets a scratch branch to the frozen baseline tag
#      (eval-shelteradoption-baseline-2026-09-07), which already has the
#      slice built — then strips that slice's implementation + test files
#      and commits that as the run's actual starting point. Without this
#      step the agent would just find working code already in place and
#      trivially mark it Done, measuring nothing.
#   2. Hides .eventmodelers/config.json for the duration of the run, so the
#      Ralph agent never talks to the live board (no risk of colliding with
#      real Planned work sitting in the same board context — see script
#      header comment further down for why that matters) and works purely
#      from a local .slices/default/ cache this script seeds with exactly
#      one slice.
#   3. Invokes lib/agent.sh (the same entrypoint ralph.sh uses) directly, up
#      to MAX_ITERATIONS times, resetting the slice back to "Planned"
#      between attempts if the agent didn't reach Done — this is what
#      "iterations_to_green" measures. ralph.sh's own onPlannedSlice loop
#      does NOT retry a stalled InProgress slice (backend-prompt.md only
#      picks up slices that are exactly "Planned"), so a bare ralph.sh
#      invocation would silently stop after one failed attempt; this
#      script's retry-with-reset is deliberate, not a bug workaround.
#   4. Runs the mechanical gate (dotnet build + the slice's own tests)
#      independently of whatever the agent self-reports, for a ground-truth
#      passed_mechanical value.
#   5. Appends one JSONL row to eval/results.jsonl.
#
# The run branch is left checked out afterward for hand review (workstream
# 4) — this script never merges to dev and never pushes anything.
#
# Usage: ./run-slice.sh <sliceFolder> [maxIterations]
#   sliceFolder    — the "folder" key from slices-manifest.json (e.g. ReviewApplication)
#   maxIterations  — default 3

set -euo pipefail

KIT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
REPO_ROOT="$(cd "$KIT_DIR/.." && pwd)"
# NOTE: ralph.sh's own default PROJECT_DIR is "$KIT_DIR/code/K9Crush-scaffold/K9Crush",
# but build-kit-dotnet/code/ does not exist on disk — the real code lives at
# $REPO_ROOT/code/K9Crush-scaffold/K9Crush (a sibling of build-kit-dotnet/, matching
# ralph.sh's own header comment, just not its actual default value). Confirmed by
# checking for K9Crush.sln directly. Using the correct path here; this is a
# pre-existing inconsistency in ralph.sh itself, not something this harness should
# silently paper over — worth a note in the eval write-up (workstream 5).
PROJECT_DIR="$REPO_ROOT/code/K9Crush-scaffold/K9Crush"
EVAL_DIR="$KIT_DIR/eval"
MANIFEST="$EVAL_DIR/slices-manifest.json"
RESULTS="$EVAL_DIR/results.jsonl"
BASELINE_TAG="eval-shelteradoption-baseline-2026-09-07"
CONFIG_FILE="$REPO_ROOT/.eventmodelers/config.json"
CONFIG_HIDDEN="$REPO_ROOT/.eventmodelers/config.json.eval-hidden"

SLICE_FOLDER="${1:?Usage: run-slice.sh <sliceFolder> [maxIterations] — see eval/slices-manifest.json for valid folders}"
MAX_ITERATIONS="${2:-3}"

# --- Resolve the slice's manifest entry -------------------------------------
# Each field is fetched with its own node -e call rather than parsed out of one
# tab-separated line with `read` — `read`'s word-splitting breaks on any field
# containing a space, which every slice title here does ("Review Application").

manifest_field() {
  node -e "
    const m = require('$MANIFEST');
    const s = m.slices.find(x => x.folder === process.argv[1]);
    if (!s) { console.error('Unknown slice folder: ' + process.argv[1]); process.exit(1); }
    const v = s[process.argv[2]];
    process.stdout.write(typeof v === 'string' ? v : JSON.stringify(v));
  " "$SLICE_FOLDER" "$1"
}

SLICE_ID="$(manifest_field boardId)"
SLICE_TITLE="$(manifest_field title)"
SLICE_TYPE="$(manifest_field sliceType)"
IMPL_DIR="$(manifest_field implDir)"
TEST_FILES_JSON="$(manifest_field testFiles)"

if [[ -z "$SLICE_ID" ]]; then
  exit 1  # manifest_field already printed the error
fi

# ralph.js/backend-prompt.md derive the on-disk slice folder from the TITLE,
# not from our manifest key — must match exactly or the agent won't find it.
RALPH_FOLDER=$(node -e "console.log(process.argv[1].replaceAll(' ', '').toLowerCase())" "$SLICE_TITLE")

echo "[eval] slice: $SLICE_TITLE  ($SLICE_ID, $SLICE_TYPE)"
echo "[eval] manifest folder: $SLICE_FOLDER  ralph folder: $RALPH_FOLDER"

# --- Preconditions ------------------------------------------------------------

# -uno: untracked files (this eval/ dir itself, .slices/, credentials) never
# block a run — they survive `git checkout -B` regardless of target commit.
# Only uncommitted changes to already-tracked files should block.
if [[ -n "$(cd "$REPO_ROOT" && git status --porcelain -uno)" ]]; then
  echo "[eval] ERROR: repo has uncommitted changes to tracked files — commit, stash, or discard before running the harness." >&2
  exit 1
fi

# --- Step 1: reset to baseline, strip the slice's implementation -------------

RUN_BRANCH="eval/$SLICE_FOLDER"
cd "$REPO_ROOT"
git checkout -B "$RUN_BRANCH" "$BASELINE_TAG"

echo "[eval] stripping $IMPL_DIR and its tests for a pre-build baseline"
rm -rf "$PROJECT_DIR/$IMPL_DIR"
git add -A -- "$PROJECT_DIR/$IMPL_DIR" 2>/dev/null || true

node -e "
const files = $TEST_FILES_JSON;
const fs = require('fs');
for (const f of files) {
  const p = '$PROJECT_DIR/' + f;
  if (fs.existsSync(p)) fs.unlinkSync(p);
}
"
for f in $(node -e "console.log($TEST_FILES_JSON.join(' '))"); do
  git add -A -- "$PROJECT_DIR/$f" 2>/dev/null || true
done

git commit -m "eval: strip $SLICE_TITLE for harness baseline (pre-build state)

Not real product work — see eval/README in this dir. Reverted by resetting
this branch to $BASELINE_TAG." --quiet

echo "[eval] baseline commit for this run: $(git rev-parse --short HEAD)"

# --- Step 2: hide board credentials for the duration of the run --------------

RESTORE_CONFIG=false
if [[ -f "$CONFIG_FILE" ]]; then
  mv "$CONFIG_FILE" "$CONFIG_HIDDEN"
  RESTORE_CONFIG=true
fi
trap '[[ "$RESTORE_CONFIG" == true ]] && mv "$CONFIG_HIDDEN" "$CONFIG_FILE"' EXIT

# --- Step 3: seed a local-only .slices/default/ cache with just this slice ---

SLICES_DIR="$KIT_DIR/.slices"
rm -rf "$SLICES_DIR"
mkdir -p "$SLICES_DIR/default/$RALPH_FOLDER"

cat > "$SLICES_DIR/current_context.json" <<JSON
{ "name": "default" }
JSON
cat > "$SLICES_DIR/default/context.json" <<JSON
{ "name": "default" }
JSON

seed_index_json() {
  local status="$1"
  node -e "
    const fs = require('fs');
    const entry = {
      id: '$SLICE_ID',
      slice: '$SLICE_TITLE',
      index: 0,
      contextName: 'default',
      contextSlug: 'default',
      folder: '$RALPH_FOLDER',
      status: '$status',
      definition: { id: '$SLICE_ID', title: '$SLICE_TITLE', status: '$status' },
    };
    fs.writeFileSync('$SLICES_DIR/default/index.json', JSON.stringify({ slices: [entry] }, null, 2));
    fs.writeFileSync('$SLICES_DIR/default/$RALPH_FOLDER/slice.json', JSON.stringify({
      id: '$SLICE_ID', sliceType: '$SLICE_TYPE', status: '$status', title: '$SLICE_TITLE',
    }, null, 2));
  "
}
seed_index_json "Planned"

echo "[]" > "$KIT_DIR/tasks.json"  # keep ralph's onTask loop from ever firing

# --- Step 4: run the agent, up to MAX_ITERATIONS, retrying on non-Done -------

BACKEND_PROMPT="$(cat "$KIT_DIR/lib/backend-prompt.md")"
START_TS=$(date +%s)
ITER=0
FINAL_STATUS="never-attempted"

while [[ "$ITER" -lt "$MAX_ITERATIONS" ]]; do
  ITER=$((ITER + 1))
  echo "[eval] === iteration $ITER/$MAX_ITERATIONS ==="
  set +e
  (cd "$PROJECT_DIR" && bash "$KIT_DIR/lib/agent.sh" "$BACKEND_PROMPT")
  set -e

  CUR_STATUS=$(node -e "
    const d = require('$SLICES_DIR/default/index.json');
    process.stdout.write((d.slices[0] && d.slices[0].status) || 'unknown');
  ")
  echo "[eval] status after iteration $ITER: $CUR_STATUS"

  if [[ "$CUR_STATUS" == "Done" ]]; then
    FINAL_STATUS="Done"
    break
  fi

  FINAL_STATUS="$CUR_STATUS"
  # Reset to Planned for the next attempt (see header comment — ralph's own
  # loop won't do this on its own for a stalled InProgress slice).
  seed_index_json "Planned"
done

END_TS=$(date +%s)
WALL_CLOCK_MIN=$(node -e "console.log((($END_TS - $START_TS) / 60).toFixed(2))")

# --- Step 5: independent mechanical gate --------------------------------------

BUILD_OK=true
TEST_OK=true
TESTS_MATCHED=0
cd "$PROJECT_DIR"
if ! dotnet build K9Crush.sln > "$EVAL_DIR/last-build.log" 2>&1; then
  BUILD_OK=false
fi
if [[ "$BUILD_OK" == true ]]; then
  set +e
  dotnet test K9Crush.sln --filter "FullyQualifiedName~${SLICE_FOLDER}" > "$EVAL_DIR/last-test.log" 2>&1
  TEST_EXIT=$?
  set -e
  # `dotnet test` exits 0 even when the filter matches ZERO tests anywhere in
  # the solution (confirmed empirically) — an exit-code-only check would read
  # "the agent produced no test at all" as a pass, exactly the kind of
  # false-green the whole eval exists to catch. Sum the "Total:" counts from
  # every project's summary line instead; a run with no matched tests fails.
  # `grep` exits 1 on "no matches" (the expected outcome when the agent left
  # no tests behind) — under `set -o pipefail` that would kill the whole
  # pipeline and abort the script before a result row is ever written.
  # `|| true` on each grep keeps that a normal zero-tests reading, not a crash.
  TESTS_MATCHED=$( { grep -oE 'Total: *[0-9]+' "$EVAL_DIR/last-test.log" || true; } | { grep -oE '[0-9]+' || true; } | awk '{s+=$1} END {print s+0}')
  if [[ "$TEST_EXIT" -ne 0 || "$TESTS_MATCHED" -eq 0 ]]; then
    TEST_OK=false
  fi
else
  TEST_OK=false
fi

if [[ "$BUILD_OK" == true && "$TEST_OK" == true ]]; then
  PASSED_MECHANICAL=true
  FAILURE_CLASS="null"
elif [[ "$BUILD_OK" == false ]]; then
  PASSED_MECHANICAL=false
  FAILURE_CLASS='"compile"'
elif [[ "$TESTS_MATCHED" -eq 0 ]]; then
  PASSED_MECHANICAL=false
  FAILURE_CLASS='"no-tests-matched"'
else
  PASSED_MECHANICAL=false
  FAILURE_CLASS='"test"'
fi

ITERATIONS_TO_GREEN="null"
if [[ "$FINAL_STATUS" == "Done" ]]; then
  ITERATIONS_TO_GREEN="$ITER"
fi

# --- Step 6: append one result row --------------------------------------------

node -e "
const fs = require('fs');
const row = {
  slice_id: '$SLICE_ID',
  slice_folder: '$SLICE_FOLDER',
  slice_title: '$SLICE_TITLE',
  run_branch: '$RUN_BRANCH',
  run_timestamp: new Date().toISOString(),
  passed_mechanical: $PASSED_MECHANICAL,
  iterations_to_green: $ITERATIONS_TO_GREEN,
  agent_final_status: '$FINAL_STATUS',
  failure_class: $FAILURE_CLASS,
  wall_clock_minutes: $WALL_CLOCK_MIN,
  human_review_verdict: null,
  notes: '',
};
fs.appendFileSync('$RESULTS', JSON.stringify(row) + '\n');
"

echo "[eval] === result: passed_mechanical=$PASSED_MECHANICAL iterations_to_green=$ITERATIONS_TO_GREEN wall_clock=${WALL_CLOCK_MIN}m ==="
echo "[eval] row appended to $RESULTS"
echo "[eval] run branch left checked out: $RUN_BRANCH (not merged, not pushed)"
