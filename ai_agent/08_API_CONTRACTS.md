# 08 — API Contracts

Base: `/api`  
Auth: `Authorization: Bearer <token>`  
Format: JSON; dates ISO-8601 timestamptz  
Errors: RFC7807 ProblemDetails

## Auth

| Method | Path | Role | Body / Notes |
|--------|------|------|--------------|
| POST | `/auth/login` | Anon | `{ userName, password }` → `{ accessToken, refreshToken, expiresIn, role, displayName }` |
| POST | `/auth/refresh` | Anon | `{ refreshToken }` |
| POST | `/auth/logout` | Auth | invalidate refresh |
| GET | `/auth/me` | Auth | profile + role |

## Users (Admin)

| Method | Path | Notes |
|--------|------|-------|
| GET | `/users` | query: role, search, page |
| POST | `/users` | create Admin/Teacher + optional teacherProfile |
| PUT | `/users/{id}` | update active, email |
| POST | `/users/{id}/reset-password` | |

## Facilities

| Method | Path | Role |
|--------|------|------|
| GET/POST | `/facilities` | Admin (GET also Teacher read) |
| GET/PUT/DELETE | `/facilities/{id}` | Admin |

## Teachers

| Method | Path | Role |
|--------|------|------|
| GET | `/teachers` | Admin |
| GET | `/teachers/{id}` | Admin |
| POST | `/teachers` | Admin — creates user+profile |
| PUT | `/teachers/{id}` | Admin |
| GET | `/teachers/me` | Teacher |
| GET | `/teachers/me/schedule` | `?from=&to=` |
| GET | `/teachers/{id}/documents` | Admin / self |
| POST | `/teachers/{id}/documents` | multipart Admin |
| DELETE | `/teachers/{id}/documents/{docId}` | Admin |

## Students

| Method | Path | Role |
|--------|------|------|
| GET | `/students` | Admin; Teacher filtered |
| POST | `/students` | Admin |
| GET/PUT | `/students/{id}` | Admin; Teacher GET if scoped |
| GET/POST | `/students/{id}/documents` | Admin |
| DELETE | `/students/{id}/documents/{docId}` | Admin |
| GET | `/students/{id}/attendance` | `?from=&to=` |
| GET | `/students/{id}/progress` | metrics + series + comments |

## Class Groups

| Method | Path | Role |
|--------|------|------|
| GET/POST | `/class-groups` | Admin |
| GET/PUT/DELETE | `/class-groups/{id}` | Admin |
| PUT | `/class-groups/{id}/students` | `{ studentIds: [] }` replace/add |

## Scheduling

| Method | Path | Role |
|--------|------|------|
| GET | `/sessions` | Admin: filters teacherId, facilityId, from, to; Teacher: own only |
| POST | `/sessions` | Admin — body + `force?: bool` → session + conflicts |
| PUT | `/sessions/{id}` | Admin |
| DELETE | `/sessions/{id}` | soft delete / cancel |
| POST | `/sessions/check-conflicts` | dry-run conflict detection |
| POST | `/schedules/clone-week` | `{ sourceWeekStart, targetWeekStart, teacherId?, force?, skipOnError? }` |

### Session DTO (create)

```json
{
  "classGroupId": "uuid?",
  "teacherProfileId": "uuid",
  "facilityId": "uuid",
  "startAt": "2026-09-08T08:30:00+07:00",
  "endAt": "2026-09-08T10:30:00+07:00",
  "title": "Lớp A",
  "notes": null,
  "studentIds": ["uuid?"],
  "force": false
}
```

## Attendance

| Method | Path | Role |
|--------|------|------|
| GET | `/sessions/{id}/attendance` | Teacher assigned / Admin |
| PUT | `/sessions/{id}/attendance/draft` | save partial |
| POST | `/sessions/{id}/attendance/finalize` | require comments |

### Finalize body

```json
{
  "items": [
    {
      "studentId": "uuid",
      "status": "Present",
      "comment": "Hôm nay tập trung tốt hơn...",
      "moodOrLevel": 4,
      "tags": ["attention", "speech"]
    }
  ]
}
```

## Billing

| Method | Path | Role |
|--------|------|------|
| GET/POST | `/tuition-rates` | Admin |
| PUT/DELETE | `/tuition-rates/{id}` | Admin |
| POST | `/tuition-rates/resolve` | preview `{ studentId, sessionId }` |
| GET | `/invoices` | filter student, year, month |
| POST | `/invoices/generate` | `{ studentId, year, month, regenerate?: true }` |
| GET | `/invoices/{id}` | include lines |
| POST | `/invoices/{id}/void` | |

## Files

| Method | Path | Notes |
|--------|------|-------|
| GET | `/files/{**path}` | authorized stream; hoặc signed URL |

---

## Pagination standard

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 100,
  "totalPages": 5
}
```

## Common query params

`page`, `pageSize`, `search`, `from`, `to`, `sort`
