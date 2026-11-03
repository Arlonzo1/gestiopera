# Implementation Plan

## Phase 1 - Foundation

- [x] Solution and modular monolith.
- [x] Docker Compose and PostgreSQL.
- [x] Tenant, Identity and roles.
- [x] JWT, refresh tokens and initial authorization.
- [x] Backend tenant resolution and isolation.
- [x] Centralized error handling and structured logging.
- [x] Initial migration and development seed.
- [x] Initial tenant isolation tests.
- [x] Smoke test against real PostgreSQL.
- [ ] GitHub Actions CI.

## Phase 2 - Products, inventory and purchases

- [x] Product and minimal category model.
- [x] Inventory movement ledger.
- [x] Role-protected inventory adjustment.
- [x] ProductsInventory migration.
- [x] Initial projection, negative-stock and inventory-isolation tests.
- [ ] PostgreSQL concurrency and negative-stock tests.
- [x] Atomic purchases with weighted-average cost.
- [ ] Physical counts.
- [ ] Stock status transition alerts.

## Phase 3 - POS, sales and payments

- [x] Product sales with historical snapshots.
- [x] Split payments model and validation.
- [x] Atomic inventory decrement.
- [x] Sale cancellation with reversing movements.
- [x] SalesPayments migration.
- [ ] Cash shift integration.
- [ ] Receipt PDF generation.
- [ ] Audit and notification events.

## Phase 4 - Billiards tables, sessions and consumptions

- [x] Tenant-owned billiard tables and active sessions.
- [x] Start, pause, resume and list table endpoints.
- [x] Consumption snapshots and stock decrement on close.
- [x] Transactional session closing into a sale.
- [x] SignalR tenant group for real-time table updates.
- [x] Billiards migration and integration tests.
- [ ] React/PWA billiards screen and SignalR client.
- [ ] Cash shift linkage, audit events and notifications.
## Later phases

- [ ] Billiard tables and sessions.
- [ ] Cash management and expenses.
- [ ] Dashboard and reporting.
- [ ] PDFs.
- [ ] Push notifications.
- [ ] WhatsApp Cloud API.
- [ ] Audit hardening and deployment readiness.




