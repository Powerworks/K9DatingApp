## 10. Key Architecture Decisions (ADR summary — full ADRs in `docs/adr/`)

| ADR | Decision | Status |
|---|---|---|
| ADR-001 | Modular monolith over microservices for MVP | Proposed |
| ADR-002 | Mediator/messaging library: Wolverine (with `WolverineFx.Marten` outbox/inbox) | **Decided** |
| ADR-003 | Marten multi-schema-per-module vs multi-database-per-module | Proposed: schema-per-module |
| ADR-004 | Blazor render mode: Server vs WASM vs Auto | Proposed: Auto (per-component) |
| ADR-005 | Identity: self-hosted ASP.NET Identity vs external IdP | To be decided in Phase 0 |
| ADR-006 | Container platform: Kubernetes vs Azure Container Apps | Depends on org's existing platform |
| ADR-007 | STorage: minio
UPgrade to .NET 10

