# Wallow Project Overview

## Purpose
Wallow is a **production-ready .NET modular monolith base platform** with multi-tenancy support. It demonstrates Clean Architecture, DDD, CQRS, and message-based communication in a commercially viable foundation that teams can fork and extend.

## Architecture
- **Modular Monolith**: Autonomous bounded contexts (modules) communicating via events over RabbitMQ
- **Clean Architecture** per module: Domain → Application → Infrastructure → Api layers
- **Modules**: Identity, Billing, Email, Notifications

## Tech Stack
| Purpose | Technology |
|---------|------------|
| Framework | .NET 10 |
| Database | PostgreSQL 18 |
| ORM | EF Core + Dapper |
| CQRS & Messaging | Wolverine (mediator + RabbitMQ transport) |
| Real-time | SignalR |
| Identity Provider | Keycloak 26 |
| Validation | FluentValidation |
| Testing | xUnit, Testcontainers, FluentAssertions |

## Project Status
The project is currently in **early development** - foundation structure exists but full module implementation is not yet started. The implementation plan has 9 phases with 53 total tasks.

## Key Documentation
- Design document: `docs/plans/2026-02-04-wallow-pivot-design.md`
- Keycloak design: `docs/plans/2026-02-05-keycloak-integration-design.md`
- Developer guide: `docs/DEVELOPER_GUIDE.md`
- Forking guide: `docs/FORKING_GUIDE.md`
