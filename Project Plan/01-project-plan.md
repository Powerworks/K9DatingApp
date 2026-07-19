# Project Plan — "PawMatch" Dog Dating Platform

## 1. Document Purpose
This plan defines scope, phasing, timeline, team structure, and risks for building a .NET modular monolith backend (vertical slice architecture, Marten/PostgreSQL, RabbitMQ, Redis) with a Blazor frontend, deployed to a containerized platform via GitHub Actions.

## 2. Assumptions
Since specific features weren't provided, this plan assumes a standard dog-dating feature set. Adjust Section 4 if scope differs.

| # | Assumption |
|---|---|
| A1 | Modules in scope: Identity, Dog Profiles, Discovery/Matching, Chat, Notifications, Media, Subscriptions, Moderation |
| A2 | Target runtime: .NET 8 LTS (upgrade path to .NET 9 noted, not required) |
| A3 | Blazor Web App (Interactive Server + WASM "auto" render mode) is the frontend, hosted inside the same solution |
| A4 | Single Postgres cluster, one Marten-managed schema per module (logical separation) |
| A5 | Target container platform: Kubernetes (AKS/EKS/GKE) or Azure Container Apps — plan is portable to either |
| A6 | Team size: 4–6 engineers (2 backend, 1 frontend/Blazor, 1 DevOps/platform, 1 QA, 1 shared PM/tech lead) |

## 3. Objectives
- Ship an MVP dog-dating product (profile creation, swipe-based matching, real-time chat, push/email notifications) within a modular monolith that can later be split into services if needed.
- Establish a repeatable, automated path from commit → container image → deployed environment.
- Keep module boundaries clean enough that any module could be extracted into its own service without a rewrite.

## 4. Scope

### In scope (MVP)
- User registration/auth (owners), dog profile creation with photos
- Location-based discovery and swipe/like matching
- Mutual-match triggered real-time chat
- Notification delivery (email + push) for matches/messages
- Basic moderation/reporting queue
- Free vs. premium subscription gating (feature flags, no full payment gateway integration required for MVP — stub provider)
- CI/CD pipeline, container images, IaC for one environment (staging) + production promotion

### Out of scope (MVP)
- Native mobile apps (Blazor is web-first; PWA support is a stretch goal)
- Full payment processing (Stripe integration deferred to Phase 5+)
- Video calling
- Multi-region active-active deployment

## 5. Team & Roles

| Role | Responsibility |
|---|---|
| Tech Lead / Architect | Module boundaries, ADRs, code review gate on cross-module changes |
| Backend Engineer(s) | Vertical slices, Marten schema, MassTransit integration |
| Frontend Engineer | Blazor components, SignalR client, UX |
| DevOps/Platform Engineer | Dockerfiles, Helm/Bicep/Terraform, GitHub Actions, observability stack |
| QA Engineer | Test strategy, integration/E2E suites, release sign-off |
| Product/PM | Backlog, acceptance criteria, phase sign-off |

## 6. Phased Delivery Plan

| Phase | Name | Duration | Goal |
|---|---|---|---|
| 0 | Discovery & Architecture | 2 weeks | Finalize module boundaries, ADRs, environment strategy, repo scaffolding |
| 1 | Platform Foundation | 3 weeks | Solution skeleton, Marten/Postgres, RabbitMQ/MassTransit, Redis, Docker Compose dev env, base CI pipeline |
| 2 | Core Identity & Profiles | 3 weeks | Auth (ASP.NET Identity or external IdP), Dog Profile CRUD vertical slices, Media upload |
| 3 | Discovery & Matching | 3 weeks | Geo-query discovery feed, swipe/like slice, match event → integration flow |
| 4 | Chat & Notifications | 3 weeks | SignalR chat backed by Redis backplane, Notification module consuming match/message events |
| 5 | Moderation, Subscriptions, Hardening | 3 weeks | Reporting/moderation slices, subscription gating, load/security testing, observability |
| 6 | Beta & Launch Prep | 2 weeks | Staging soak test, CD to production pipeline, runbooks, rollback drills |

**Total estimate: ~19 weeks** (≈4.5 months) for MVP with the assumed team size. Contingency of 15% recommended.

### Milestone Gantt (indicative, weeks)

```
Phase 0 |==|
Phase 1    |===|
Phase 2        |===|
Phase 3            |===|
Phase 4                |===|
Phase 5                    |===|
Phase 6                        |==|
Week:   1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19
```

## 7. Deliverables per Phase

| Phase | Key Deliverables |
|---|---|
| 0 | Architecture doc (this set), ADR log, repo + branch strategy, environment list |
| 1 | Buildable solution, docker-compose for local infra, first green GitHub Actions pipeline, base module template |
| 2 | Identity module, Profile module, media storage integration, Blazor auth flow |
| 3 | Matching module, geo-indexed discovery feed, integration events (`DogLiked`, `MatchCreated`) |
| 4 | Chat module with SignalR, Notification module, email/push provider integration |
| 5 | Moderation module, Subscription module, load test report, dashboards/alerts |
| 6 | Production deployment pipeline, runbooks, go-live checklist |

## 8. Risks & Mitigations

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Module boundaries leak (tight coupling reintroduced) | High | Medium | Enforce via architecture tests (e.g., NetArchTest) in CI; code review checklist |
| RabbitMQ/outbox misconfiguration causes lost events | High | Medium | Use transactional outbox pattern with Marten; integration tests for event delivery |
| Marten schema evolution breaks running system | Medium | Medium | Adopt document versioning/upcasting conventions from day one |
| SignalR + Redis backplane scaling issues under chat load | Medium | Low | Load test chat early (Phase 4), design for horizontal scale-out |
| Team unfamiliarity with vertical slice/Marten patterns | Medium | Medium | Phase 0 spike + internal brown-bag/documentation, pair programming |
| Scope creep on "dating app" features | Medium | High | Lock MVP scope in Phase 0 sign-off; changes go through backlog, not mid-phase |
| Container platform choice changes mid-project | Medium | Low | Keep IaC platform-agnostic (Docker images + Helm charts work across K8s providers) |

## 9. Definition of Done (per module)
- Vertical slices implemented with unit + integration tests (>80% coverage on domain logic)
- Marten document/event schema documented
- Published/consumed integration events documented in the module's README
- OpenAPI spec generated and reviewed
- Passes architecture fitness tests in CI
- Dashboards/alerts wired for new module's key metrics

## 10. Success Metrics (MVP)
- P95 API latency < 300ms for discovery/profile endpoints
- Chat message delivery latency < 500ms P95
- Pipeline: commit-to-staging-deploy < 15 minutes
- Zero cross-module compile-time references outside published contracts
