# Billares POS

Multi-tenant SaaS foundation for billiard hall/bar administration.

## Prerequisites

- .NET SDK 10
- Docker Desktop

## Local setup

Create a local `.env` from `.env.example` and start PostgreSQL/API:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

Or run PostgreSQL only and start the API from the SDK:

```powershell
docker compose up -d postgres
dotnet run --project src/BillaresPos.Api/BillaresPos.Api.csproj
```

OpenAPI is available in Development at:

```text
http://localhost:8080/openapi/v1.json
```

Health check:

```text
GET http://localhost:8080/health
```

## Development seed

When the API starts in Development it applies migrations and creates:

- Tenant: `Billar Demo` with slug `billar-demo`
- Owner: `owner@billar-demo.local`
- Password: `DemoOwner123!`
- Roles: `OWNER`, `ADMINISTRATOR`, `CASHIER`

The development seed creates four billiard tables: `Mesa 1` through `Mesa 4`, each with a 20,000 COP hourly rate and 15-minute billing increments.

## Login

```http
POST /api/auth/login
Content-Type: application/json

{
  "tenantSlug": "billar-demo",
  "email": "owner@billar-demo.local",
  "password": "DemoOwner123!"
}
```

Use the returned access token as `Authorization: Bearer <token>`.

## Tests

```powershell
dotnet test
```

## Frontend

Run the API and PostgreSQL in one terminal, then start the React PWA in another:

```powershell
dotnet run --project src/BillaresPos.Api --urls http://localhost:5080
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. The development frontend uses the seeded account and consumes the billiard table API and SignalR hub.

## Current phase

Implemented: backend foundation, products, inventory, purchases, POS, payments and billiard sessions. The React/PWA billiard board is now available. Pending: cash management, reports, PDFs, push, WhatsApp and auditing hardening.


