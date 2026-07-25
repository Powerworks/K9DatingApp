#!/usr/bin/env node
// Ralph loop + realtime agent using Claude Code as the executor.
// Usage: node ralph-claude.js [project_dir]

import { startRalph, loadLocalConfig } from './lib/ralph.js';
import { spawn } from 'child_process';
import { dirname, resolve } from 'path';
import { fileURLToPath } from 'url';

const kitDir = dirname(fileURLToPath(import.meta.url));
// This is a generic template — unlike the K9Crush-derived build-kit-dotnet
// it was forked from, there's no fixed relative path to guess at (every
// consuming repo places its .NET solution differently). Pass project_dir
// explicitly, or set DOTNET_PROJECT_DIR once per repo.
const projectDirArg = process.argv[2] || process.env.DOTNET_PROJECT_DIR;
if (!projectDirArg) {
  console.error('[ralph] Missing project_dir: pass it as an argument (node ralph-claude.js /path/to/solution) or set DOTNET_PROJECT_DIR.');
  process.exit(1);
}
const projectDir = resolve(projectDirArg);

const cfg = loadLocalConfig(kitDir);
const inlineHeader = cfg.boardId
  ? `board=${cfg.boardId} token=${cfg.token} org=${cfg.organizationId} baseUrl=${cfg.baseUrl}\n\n`
  : '';

const claudeArgs = ['--dangerously-skip-permissions'];
if (cfg.model) claudeArgs.push('--model', cfg.model);
const claudeEnv = cfg.anthropicBaseUrl
  ? { ...process.env, ANTHROPIC_BASE_URL: cfg.anthropicBaseUrl }
  : process.env;

function runClaude(prompt) {
  return new Promise((resolve, reject) => {
    const proc = spawn('claude', [...claudeArgs, '-p', inlineHeader + prompt], {
      cwd: projectDir,
      stdio: 'inherit',
      env: claudeEnv,
    });
    proc.on('close', (code) => (code === 0 ? resolve() : reject(new Error(`Claude exited ${code}`))));
    proc.on('error', reject);
  });
}

startRalph({
  kitDir,
  projectDir,
  onTask: runClaude,
  onPlannedSlice: runClaude,
}).catch((err) => {
  console.error('[ralph] Fatal:', err);
  process.exit(1);
});
