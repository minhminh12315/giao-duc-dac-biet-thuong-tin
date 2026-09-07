# 06 — Modules Specification

## Module A — Identity & Admin Accounts

**Mục tiêu:** Login, tạo tài khoản, phân role.

**Features:**
- Login / logout / refresh
- Admin CRUD users
- Reset password (Admin)
- Seed first Admin

**UI:**
- Login page
- Admin → Users list

---

## Module B — Facility Management

**Mục tiêu:** Quản lý cơ sở chính + trường đối tác.

**Features:**
- CRUD Facility
- Type filter
- Soft deactivate

**UI:** Admin → Facilities table

**Validation:** Name required; Type required.

---

## Module C — Teacher Management

### C1 Profile & Documents
- CRUD TeacherProfile (+ linked User)
- Upload/list/delete documents (Contract, Certificate, Other)
- Schema lưu `storage_path` + metadata (xem `10_FILE_STORAGE.md`)

### C2 Weekly Schedule (Teacher view)
- GET sessions của teacher trong khoảng tuần
- UI calendar read-only (Teacher) / link vào điểm danh

### C3 Teaching History & Feedback
- Mở session → list SessionStudents
- Set AttendanceStatus từng HS
- Comment bắt buộc khi Finalize
- Optional MoodOrLevel 1–5
- Lịch sử các ca đã dạy + comments

**States điểm danh:**
1. Draft (NotMarked allowed)
2. Finalize → Session.Status=Completed, AttendanceFinalizedAt set
3. Post-edit: Admin hoặc trong EditWindowHours

---

## Module D — Student Management

### D1 Student Profile
- CRUD Student
- Upload medical/assessment docs
- Status Active/Inactive/Graduated

### D2 Facility Assignment
- `Student.FacilityId` bắt buộc
- UI select Facility (Main hoặc Partner)
- Hiển thị badge loại cơ sở trên profile

### D3 Attendance & Progress
- Timeline attendance từ AttendanceRecord
- Progress view:
  - Số buổi Present / Absent / Excused theo tháng
  - Line chart MoodOrLevel theo thời gian (nếu có)
  - List comments chronological (filter by date, teacher)
- Export CSV optional (phase 2)

---

## Module E — Scheduling (Admin)

### E1 Dashboard lịch chung
- Week calendar toàn hệ thống
- Filters: Teacher, Facility, ClassGroup
- Color-code theo Facility hoặc Teacher

### E2 Scheduling UI (bắt buộc Calendar/Time-block Grid)
- Cột = ngày trong tuần
- Hàng = khung giờ (ví dụ 30 phút slots từ WorkDayStart–WorkDayEnd)
- Drag-create hoặc click-empty → form tạo Session:
  - Teacher, ClassGroup, Facility, Start/End, Title
- Click session → edit / cancel / mở attendance (Admin)
- Conflict badges trên session card

### E3 Clone / Copy tuần
- Chọn SourceWeek + TargetWeek
- Options:
  - Copy all / filter by teacher
  - Skip cancelled
  - On conflict: Skip | Still copy (with warnings)
- Result report: created count, skipped, warnings

### E4 Automation helpers
- Snapshot ClassGroup students → SessionStudents on create
- Detect overlap + travel time (algorithm file 07)

---

## Module F — Billing & Tuition

### F1 Rate Configuration
- UI CRUD TuitionRate theo Scope:
  - SystemDefault
  - ServiceType (AtCenter / AtPartnerSchool)
  - ClassGroup
  - Student
- Effective date range
- Preview “resolved rate” cho 1 student + session context

### F2 Dynamic Billing
- Admin chọn Student + Month/Year → Generate Invoice
- Algorithm: file 07
- Xem Invoice + lines (session datetime, rate, amount)
- Regenerate / Void

### F3 Reports (MVP light)
- Tổng học phí theo tháng (all students)
- Breakdown by Facility / ServiceType

---

## Module G — Documents (shared)

- Upload endpoint multipart
- Download/stream with authz
- Delete (Admin; soft or hard file + DB row)

---

## Cross-module data flow

```
Admin xếp Session
    → Teacher xem lịch
    → Teacher điểm danh + comment
    → Attendance Present
    → Billing engine đọc Present + resolve Rate
    → Invoice
Comments
    → Student Progress charts
```
