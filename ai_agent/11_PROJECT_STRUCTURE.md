# 11 — Project Structure

## Monorepo layout (khuyến nghị)

```
giao_duc_dac_biet_thuong_tin/
  ai_agent/                          ← spec (thư mục này)
  docker-compose.yml
  README.md
  src/
    backend/
      Secms.sln
      Secms.Api/
        Controllers/
        Middleware/
        Program.cs
        appsettings.json
        appsettings.Development.json
      Secms.Application/
        Auth/
        Facilities/
        Teachers/
        Students/
        Scheduling/
        Attendance/
        Billing/
        Common/
          Interfaces/
          Models/
          Behaviors/
      Secms.Domain/
        Entities/
        Enums/
        Exceptions/
        Services/          ← domain services: RateResolver, ConflictDetector
      Secms.Infrastructure/
        Persistence/
          SecmsDbContext.cs
          Configurations/
          Migrations/
        Identity/
        Storage/
        DependencyInjection.cs
      Secms.UnitTests/
      Secms.IntegrationTests/
    frontend/
      package.json
      vite.config.ts
      index.html
      src/                 ← xem 09_FRONTEND_SPEC.md
  scripts/
    seed.sh
    reset-db.sh
```

## Backend package refs

- Api → Application, Infrastructure
- Infrastructure → Application, Domain
- Application → Domain
- Tests → all needed

## Key NuGet

- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.AspNetCore.Identity.EntityFrameworkCore
- Microsoft.AspNetCore.Authentication.JwtBearer
- FluentValidation.DependencyInjectionExtensions
- Serilog.AspNetCore
- Swashbuckle.AspNetCore
- (optional) MediatR

## Frontend packages

- vue, vue-router, pinia, axios, vue-i18n
- element-plus (or naive-ui)
- @fullcalendar/vue3 + timegrid/interaction
- echarts hoặc chart.js
- dayjs (timezone plugin)

## Docker Compose (sketch)

```yaml
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_USER: secms
      POSTGRES_PASSWORD: secms
      POSTGRES_DB: secms
    ports: ["5432:5432"]
    volumes: ["pgdata:/var/lib/postgresql/data"]
  api:
    build: ./src/backend/Secms.Api
    environment:
      ConnectionStrings__Default: Host=db;Database=secms;Username=secms;Password=secms
    ports: ["8080:8080"]
    depends_on: [db]
  web:
    build: ./src/frontend
    ports: ["5173:80"]
    depends_on: [api]
volumes:
  pgdata:
```

## Coding conventions

- C#: nullable enable, file-scoped namespaces, async suffix
- API controllers mỏng; logic trong Application services
- Vue: Composition API only
- Không commit secrets; dùng User Secrets / env
- `.gitignore`: `bin/`, `obj/`, `node_modules/`, `App_Data/`, `.env`
