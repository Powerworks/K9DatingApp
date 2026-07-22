# build-kit

This is the stock Node/Express/[emmett](https://event-driven-io.github.io/emmett/)/Knex
backend scaffold that the eventmodelers.ai "code export" tool generates —
dropped in here untouched, before any project-specific adaptation.

**It is not what K9Crush is actually built on.** The real implementation
lives in `code/K9Crush-scaffold/K9Crush/` (.NET, Wolverine.Http + Marten +
RabbitMQ), and the tooling that drives *that* codegen is
[`build-kit-dotnet/`](../build-kit-dotnet/README.md) — a fork of this
folder's `.build-kit/` retargeted at the .NET stack. Everything in here
(routes, the `swagger.ts` description, the migrations) is still the
original template's placeholder content ("shift, clerk, and task
management") and hasn't been renamed to K9Crush.

Kept around for reference — e.g. if a future slice's target stack switches
back to Node, or to compare the two Ralph loops' behavior.

## Layout

- `server.ts`, `src/` — the Express app itself (emmett event store, Knex,
  Swagger)
- `migrations/V1__schema.sql.example` — Flyway migration example (rename
  off `.example` to use)
- `docker-compose.yml` — local Postgres for this app
- `.build-kit/` — Ralph loop + Claude skills, gitignored here (see
  `build-kit-dotnet/README.md` for the maintained copy; run
  `cat .build-kit/README.md` locally if you need this original's docs)
- `CLAUDE.md` — codegen rules for slices built directly in this folder

## Running

```bash
cp .env.example .env        # or: ./setup-env.sh for an interactive prompt
docker compose up -d        # starts local Postgres
npm install
npm run flyway:migrate      # apply migrations/*.sql
npm run dev                 # starts the Express server (see .env PORT)
```

Other scripts: `npm run build` (tsc), `npm test` (runs `src/**/*.test.ts`).
