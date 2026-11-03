# Billares POS Architecture

## Scope

This repository starts a multi-tenant SaaS for billiard halls and bars. The first iteration implements only Phase 1: foundation, tenancy, Identity, JWT authentication, PostgreSQL persistence, centralized errors, structured logging, Docker Compose, seed data for tenant/user/roles, and initial integration tests.

## Repository layout

- `src/BillaresPos.Api`: ASP.NET Core Web API composition root, HTTP endpoints, auth, tenant middleware, error middleware.
- `src/BillaresPos.Domain`: domain types shared across modules. Kept framework-light.
- `src/BillaresPos.Application`: application contracts and cross-cutting abstractions such as `ICurrentTenant`, `ICurrentUser`, `IClock`.
- `src/BillaresPos.Infrastructure`: EF Core, PostgreSQL, Identity persistence, development seed data, production implementations of infrastructure contracts.
- `tests/BillaresPos.IntegrationTests`: HTTP-level integration tests.

## Modular monolith direction

Future business modules should be organized by feature/module under these projects, for example `Products`, `Inventory`, `Sales`, `Billiards`, and `CashManagement`. They should share one deployment unit and one database, with strict module boundaries in code and transactional consistency where sales/inventory/cash interact.

## Tenant isolation decision

Tenant-owned EF entities implement `ITenantOwned` and receive global query filters bound to `ICurrentTenant`. Authenticated requests resolve the tenant from the signed JWT `tenant_id` claim. Endpoints must not accept tenant IDs from the frontend for authorization decisions.

Login and refresh are special cases: they query with `IgnoreQueryFilters()` because no authenticated tenant exists yet. They explicitly constrain by tenant slug/token hash and active user status.

## Assumptions

- Initial model is one tenant per establishment. Multi-branch will be added later with a branch/location dimension, not now.
- Default currency is COP and default tenant timezone is `America/Bogota`.
- Refresh tokens are opaque random values stored only as SHA-256 hashes and rotated on refresh.
- Development seed creates tenant, roles and owner only in Phase 1. Product/table seed waits until those modules exist.

## Key risks

- Cross-tenant IDOR: every tenant-owned query must go through filtered `DbSet`s unless there is a reviewed administrative/bootstrap reason.
- Financial/inventory consistency: later phases must use database transactions and concurrency-safe stock updates.
- Notification side effects: future PDF, WhatsApp and push work must be queued only after commit.
- Timezone boundaries: reports and business dates must convert from UTC using tenant timezone.
