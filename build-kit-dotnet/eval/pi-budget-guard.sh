#!/bin/bash
# pi-budget-guard.sh — Pi-side equivalent of budget-guard.sh (Corrective
# Project Plan WS1.3/WS1.4). Same cumulative-cost-cap + wall-clock-timeout
# contract as budget-guard.sh, but drives Pi instead of the `claude` CLI,
# for the WS1.4 baseline comparison (current setup vs Pi, same tasks, same
# models).
#
# Auth note (found 2026-09-08): Pi's stored Anthropic OAuth credential is
# invalid/expired, and it does NOT fall back to ANTHROPIC_API_KEY from the
# environment on its own — the API key must be passed explicitly via
# --api-key. This script pulls it from GCP Secret Manager
# (agentos-61847/anthropic-api-key) fresh on every call rather than caching
# it in a variable that could leak into logs/process listings for longer
# than necessary.
#
# Usage: ./pi-budget-guard.sh <state-file> <model> "<prompt>"
#   state-file — same cumulative-cost state file shape as budget-guard.sh;
#                use a SEPARATE state file from the Claude-side runs so the
#                two harnesses' budgets don't get counted against each other
#   model      — e.g. claude-sonnet-5 — must match whatever the Claude Code
#                side of the comparison is actually using, or the "same
#                models" comparison WS1.4 calls for isn't real

set -euo pipefail

STATE_FILE="${1:?Usage: pi-budget-guard.sh <state-file> <model> <prompt>}"
MODEL="${2:?Usage: pi-budget-guard.sh <state-file> <model> <prompt>}"
PROMPT="${3:?Usage: pi-budget-guard.sh <state-file> <model> <prompt>}"

MAX_COST_USD="${EVAL_MAX_COST_USD:-2.00}"
MAX_WALLCLOCK_S="${EVAL_MAX_WALLCLOCK_S:-900}"
GCP_PROJECT="${EVAL_GCP_PROJECT:-agentos-61847}"
SECRET_NAME="${EVAL_ANTHROPIC_SECRET:-anthropic-api-key}"

if [[ ! -f "$STATE_FILE" ]]; then
  echo '{"cumulative_cost_usd": 0, "calls": 0}' > "$STATE_FILE"
fi

CUR_COST=$(node -e "console.log(require('$STATE_FILE').cumulative_cost_usd)")

if node -e "process.exit($CUR_COST >= $MAX_COST_USD ? 0 : 1)"; then
  echo "[pi-budget-guard] REFUSING: cumulative cost \$$CUR_COST already at/over cap \$$MAX_COST_USD — not making this call." >&2
  exit 1
fi

echo "[pi-budget-guard] cumulative so far: \$$CUR_COST / \$$MAX_COST_USD cap. Wall-clock cap this call: ${MAX_WALLCLOCK_S}s. Model: $MODEL"

API_KEY="$(gcloud secrets versions access latest --secret="$SECRET_NAME" --project="$GCP_PROJECT")"

RAW_OUTPUT=""
CALL_EXIT=0
set +e
RAW_OUTPUT=$(timeout "${MAX_WALLCLOCK_S}s" pi --print --mode json --no-session \
  --provider anthropic --model "$MODEL" --api-key "$API_KEY" "$PROMPT")
CALL_EXIT=$?
set -e
unset API_KEY

if [[ "$CALL_EXIT" -eq 124 ]]; then
  echo "[pi-budget-guard] KILLED: wall-clock cap of ${MAX_WALLCLOCK_S}s breached (timeout exit 124)." >&2
  exit 1
fi

if [[ "$CALL_EXIT" -ne 0 ]]; then
  echo "[pi-budget-guard] pi exited non-zero ($CALL_EXIT) — passing the failure through." >&2
  exit "$CALL_EXIT"
fi

# --- Parse Pi's streaming JSONL for the final assistant turn's cost/text ----
# Schema confirmed empirically 2026-09-08 (not just documented): one JSON
# object per line; the last line with type "agent_end" carries the full
# `messages` array, whose final assistant message has `usage.cost.total`
# and a `content` array mixing `{type:"thinking",...}` and `{type:"text",...}`
# entries — only the "text" entries are the actual answer.
PARSED=$(node -e "
  const lines = process.argv[1].trim().split('\n').filter(Boolean).map(l => JSON.parse(l));
  const end = [...lines].reverse().find(l => l.type === 'agent_end');
  if (!end) { console.log(JSON.stringify({cost: 0, text: '', error: 'no agent_end line found'})); process.exit(0); }
  const lastAssistant = [...end.messages].reverse().find(m => m.role === 'assistant');
  const cost = lastAssistant?.usage?.cost?.total ?? 0;
  const text = (lastAssistant?.content || []).filter(c => c.type === 'text').map(c => c.text).join('');
  const erroredOut = lastAssistant?.stopReason === 'error';
  console.log(JSON.stringify({cost, text, erroredOut, errorMessage: lastAssistant?.errorMessage || null}));
" "$RAW_OUTPUT")

CALL_COST=$(node -e "console.log(JSON.parse(process.argv[1]).cost)" "$PARSED")
ERRORED=$(node -e "console.log(JSON.parse(process.argv[1]).erroredOut)" "$PARSED")

if [[ "$ERRORED" == "true" ]]; then
  echo "[pi-budget-guard] Pi's own turn ended in error (auth/model issue, not a budget breach): $(node -e "console.log(JSON.parse(process.argv[1]).errorMessage)" "$PARSED")" >&2
  exit 2
fi

NEW_TOTAL=$(node -e "console.log($CUR_COST + $CALL_COST)")
node -e "
  const fs = require('fs');
  const s = JSON.parse(fs.readFileSync('$STATE_FILE'));
  s.cumulative_cost_usd = $NEW_TOTAL;
  s.calls = (s.calls || 0) + 1;
  fs.writeFileSync('$STATE_FILE', JSON.stringify(s, null, 2));
"

echo "[pi-budget-guard] this call: \$$CALL_COST. cumulative now: \$$NEW_TOTAL / \$$MAX_COST_USD cap."

node -e "console.log(JSON.parse(process.argv[1]).text)" "$PARSED"

if node -e "process.exit($NEW_TOTAL >= $MAX_COST_USD ? 0 : 1)"; then
  echo "[pi-budget-guard] BREACH: this call pushed cumulative cost to \$$NEW_TOTAL, at/over cap \$$MAX_COST_USD. Signaling caller to stop." >&2
  exit 1
fi

exit 0
