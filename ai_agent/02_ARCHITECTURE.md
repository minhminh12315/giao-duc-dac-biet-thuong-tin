# 02 — Architecture

## 1. Stack

| Layer | Technology | Notes |
|-------|------------|-------|
| Backend | ASP.NET Core 8 Web API | Clean Architecture / Modular Monolith |
| ORM | EF Core 8 | PostgreSQL provider `Npgsql` |
| Auth | ASP.NET Identity + JWT Bearer | Roles: Admin, Teacher |
| Frontend | Vue 3 + TypeScript + Vite | Pinia, Vue Router, Axios |
| UI | Element Plus **hoặc** Naive UI | Calendar: FullCalendar / custom CSS grid |
| DB | PostgreSQL 16 | UUID PKs, timestamptz |
| File storage | Local disk (dev) / S3-compatible (prod) | Abstraction `IFileStorage` |
| Docs API | Swagger / OpenAPI | |

## 2. High-level Diagram

```
┌─────────────┐     HTTPS/JSON      ┌──────────────────────┐
│  Vue 3 SPA  │ ◄─────────────────► │  ASP.NET Core API    │
│  (Admin UI  │                     │  Controllers/Minimal │
│  Teacher UI)│                     │  Application Services│
└─────────────┘                     │  Domain              │
                                    │  Infrastructure      │
                                    └──────────┬───────────┘
                                               │
                         ┌─────────────────────┼─────────────────────┐
                         ▼                     ▼                     ▼
                  PostgreSQL 16          File Storage          (optional Redis)
                  (identity, ops)        (docs PDF/IMG)        cache/session
```

## 3. Backend style: Modular Monolith

Modules (bounded contexts nhẹ):

```
Identity & Access
Facilities
Teachers
Students
Scheduling
Attendance & TeachingLogs
Billing
Documents (shared kernel)
```

Mỗi module: Entities + Services + (optional) Controllers group. Shared Kernel: Result types, base entities, clock, file storage.

## 4. Layering

```
src/
  Secms.Api/              → Controllers, middleware, DI
  Secms.Application/      → Commands/Queries (CQRS nhẹ), validators, DTOs
  Secms.Domain/           → Entities, enums, domain services, invariants
  Secms.Infrastructure/   → EF, Identity, FileStorage, email stub
```

Pattern khuyến nghị:
- MediatR (optional) hoặc Application Services tường minh
- FluentValidation
- AutoMapper / Mapster (hoặc manual mapping)
- Soft delete (`IsDeleted` + `DeletedAt`) cho Student, Teacher, Session

## 5. Auth flow

1. `POST /api/auth/login` → access token (15–60m) + refresh token
2. Frontend gắn `Authorization: Bearer`
3. `[Authorize(Roles = "Admin")]` / policy-based permissions
4. Teacher resource checks: must be assigned to session

## 6. Configuration keys

```json
{
  "Scheduling": {
    "MinTravelMinutes": 30,
    "WeekStartsOn": "Monday",
    "WorkDayStart": "07:00",
    "WorkDayEnd": "18:00",
    "AllowForceSaveWithConflicts": true
  },
  "Attendance": {
    "RequireCommentOnFinalize": true,
    "EditWindowHours": 24,
    "AdminCanAlwaysEdit": true
  },
  "Billing": {
    "Currency": "VND",
    "InvoiceTimezone": "Asia/Ho_Chi_Minh"
  },
  "Storage": {
    "Provider": "Local",
    "LocalRoot": "App_Data/uploads",
    "MaxFileBytes": 10485760
  }
}
```

## 7. Environments

| Env | API | DB | Storage |
|-----|-----|-----|---------|
| Dev | localhost:5xxx | Docker Postgres | Local disk |
| Staging | VPS/Azure | Managed PG | MinIO/S3 |
| Prod | same | Managed PG + backup | S3 |

## 8. Cross-cutting

- Global exception middleware → ProblemDetails
- Correlation ID header
- Serilog → console + file
- Audit table cho Schedule/Attendance/Invoice
- CORS chỉ origin frontend

## 9. Deployment suggestion (MVP)

- Docker Compose: `api` + `web` (nginx serve SPA) + `postgres`
- EF migrations on startup (dev) / CI migrate (prod)
