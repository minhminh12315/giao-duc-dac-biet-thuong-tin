# 12 — Implementation Roadmap

## Phase 0 — Bootstrap (0.5–1 ngày)

- [ ] Tạo solution .NET 8 + 4 projects (Api/Application/Domain/Infrastructure)
- [ ] Tạo Vue 3 + Vite + TS app
- [ ] Docker Compose Postgres
- [ ] EF DbContext + Identity + JWT login stub
- [ ] Swagger + CORS
- [ ] Seed Admin user
- [ ] README chạy local

**DoD:** Login API + Vue login page OK.

---

## Phase 1 — Master data (1–2 ngày)

- [ ] Facilities CRUD
- [ ] Teachers CRUD + User link
- [ ] Students CRUD + Facility assignment
- [ ] ClassGroups + roster
- [ ] Document upload LocalFileStorage (teacher + student)
- [ ] RBAC guards frontend

**DoD:** Admin quản lý được cơ sở, GV, HS, lớp, upload PDF.

---

## Phase 2 — Scheduling core (2–3 ngày)

- [ ] Session entity + SessionStudents snapshot
- [ ] GET sessions by week + filters
- [ ] POST/PUT/DELETE sessions
- [ ] ConflictDetector (overlap + travel)
- [ ] Admin WeekCalendar UI
- [ ] Clone week API + dialog
- [ ] Teacher my-schedule read-only

**DoD:** Admin xếp lịch tuần, clone tuần, thấy warning trùng/di chuyển; Teacher xem lịch.

---

## Phase 3 — Attendance & Progress (2 ngày)

- [ ] Draft + Finalize attendance APIs
- [ ] TeachingComment required on finalize
- [ ] Attendance UI Teacher
- [ ] Student attendance history
- [ ] Progress metrics + chart + comment timeline
- [ ] Audit log finalize

**DoD:** Điểm danh + comment bắt buộc; progress đọc được từ comments/mood.

---

## Phase 4 — Billing (1–2 ngày)

- [ ] TuitionRate CRUD + resolve priority
- [ ] GenerateInvoice algorithm
- [ ] Invoice list/detail/void/regenerate
- [ ] Admin billing screens
- [ ] Unit tests RateResolver + BillingEngine

**DoD:** Invoice tháng = Present × rate đúng scope priority.

---

## Phase 5 — Hardening (1–2 ngày)

- [ ] Integration tests critical flows
- [ ] Validation + ProblemDetails chuẩn
- [ ] Serilog + basic audit
- [ ] Config MinTravelMinutes / EditWindowHours
- [ ] Soft-delete filters
- [ ] Production storage interface ready (S3 stub)
- [ ] Docker build API + Web
- [ ] Seed demo data (1 tuần lịch mẫu)

**DoD:** Demo end-to-end hybrid scenario (center + partner school).

---

## Suggested demo script

1. Tạo MainCenter + PartnerSchool C
2. Tạo Teacher T1, Students S1 (Main), S2 (Partner C)
3. ClassGroup A (AtCenter), ClassGroup B (AtPartnerSchool)
4. Rate: Student S1 = 200k; ServiceType AtPartnerSchool = 250k
5. Xếp T1: 08:30–10:30 lớp A tại Main; 10:35–12:30 lớp B tại Partner → warning travel
6. Clone tuần sau
7. Teacher điểm danh Present + comment
8. Generate invoice tháng → verify số tiền

---

## Testing priorities

| Priority | Test |
|----------|------|
| P0 | Rate resolve priority |
| P0 | Billing counts only Present |
| P0 | Finalize requires comment |
| P0 | Teacher overlap detection |
| P1 | Travel warning |
| P1 | Clone week does not copy attendance |
| P1 | Teacher cannot access other teacher attendance |
| P2 | File MIME rejection |

---

## Open decisions (mặc định đã chọn — AI follow)

| Topic | Default |
|-------|---------|
| Identity store | ASP.NET Identity + JWT |
| Calendar lib | FullCalendar timeGrid |
| UI lib | Element Plus |
| Money | VND integer-like numeric |
| Excused billing | Không tính phí |
| Room resource | Không model phòng MVP |
| Parent portal | Không |
| Force save conflicts | Cho phép Admin force |

Nếu product owner đổi decision: cập nhật file này + `01` + `07`.
