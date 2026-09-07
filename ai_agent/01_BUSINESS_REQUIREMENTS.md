# 01 — Business Requirements

## 1. Vision

Xây dựng hệ thống quản lý vận hành trung tâm giáo dục đặc biệt theo mô hình **hybrid**:
- Dạy tại **cơ sở chính** của trung tâm
- Dạy **on-site** tại các **trường đối tác**

Hệ thống phục vụ Admin vận hành (xếp lịch, học phí, hồ sơ) và Teacher thực thi ca dạy (điểm danh + nhận xét tiến trình học sinh).

## 2. Actors

| Actor | Mô tả | Mục tiêu chính |
|-------|--------|----------------|
| Admin | Quản trị viên trung tâm | Phân quyền, xếp lịch, cấu hình đơn giá, tính học phí, CRUD tài khoản |
| Teacher | Giáo viên | Xem TKB cá nhân, điểm danh theo ca, ghi nhận xét học sinh |

> MVP không có Parent. Dữ liệu progress phục vụ nội bộ Admin/Teacher.

## 3. Business Rules (BR)

### BR-AUTH
- BR-AUTH-01: Chỉ Admin tạo tài khoản (Teacher/Admin).
- BR-AUTH-02: Teacher chỉ truy cập dữ liệu liên quan ca dạy / học sinh được phân công.
- BR-AUTH-03: JWT (access + refresh) hoặc cookie-based session; bắt buộc HTTPS production.

### BR-FACILITY
- BR-FAC-01: Mỗi Facility có `type`: `MainCenter` | `PartnerSchool`.
- BR-FAC-02: Mỗi Student **bắt buộc** gắn đúng 1 Facility tại một thời điểm (có thể đổi lịch sử qua assignment history nếu cần phase 2; MVP: 1 FK hiện tại).

### BR-SCHEDULE
- BR-SCH-01: Lịch hiển thị dạng **tuần** (Mon–Sun hoặc Mon–Sat theo config), time-block grid.
- BR-SCH-02: Mỗi Session gồm: thời gian bắt đầu/kết thúc, Teacher, ClassGroup (hoặc danh sách Student), Facility, trạng thái.
- BR-SCH-03: Admin được **clone/copy** lịch từ tuần nguồn → tuần đích.
- BR-SCH-04: Hệ thống **cảnh báo** (warning, không hard-block mặc định) khi:
  - Teacher trùng giờ 2 session.
  - 2 session liền kề của cùng teacher ở 2 facility khác nhau và khoảng trống < `MinTravelMinutes` (config, default 30).
- BR-SCH-05: Soft-block tùy chọn (feature flag): Admin có thể force-save khi có warning.

### BR-ATTENDANCE
- BR-ATT-01: Điểm danh theo từng Session, từng Student trong session.
- BR-ATT-02: Mỗi student trong session **bắt buộc** có Comment khi Teacher submit attendance (có thể cho phép draft lưu tạm thiếu comment).
- BR-ATT-03: Status: `Present` | `Absent` | `Excused` (Excused = nghỉ có phép; **không tính phí** giống Absent theo công thức MVP).
- BR-ATT-04: Chỉ Teacher được assign vào session (hoặc Admin) mới điểm danh được session đó.
- BR-ATT-05: Attendance đã finalize không sửa được trừ Admin (hoặc trong window `EditHours` config).

### BR-BILLING
- BR-BIL-01: Đơn giá không cố định toàn hệ thống — cấu hình theo Student **và/hoặc** ClassGroup **và/hoặc** ServiceType (`AtCenter` | `AtPartnerSchool`).
- BR-BIL-02: Priority resolve rate (cao → thấp): StudentRate → ClassGroupRate → ServiceTypeRate → SystemDefaultRate.
- BR-BIL-03: Học phí tháng = Σ (session có AttendanceStatus = Present) × RateResolved(session, student).
- BR-BIL-04: Absent / Excused / session chưa điểm danh → **không tính phí**.
- BR-BIL-05: Invoice tháng có thể regenerate nếu attendance thay đổi (Admin-only), giữ version history.

### BR-DOCUMENTS
- BR-DOC-01: Teacher & Student documents: ảnh/PDF; lưu **URL hoặc file path** trên storage, DB chỉ lưu metadata + path.
- BR-DOC-02: Max size default 10MB; MIME whitelist: `image/jpeg`, `image/png`, `image/webp`, `application/pdf`.

### BR-PROGRESS
- BR-PRG-01: Development tracking = tổng hợp TeachingLog comments theo thời gian (timeline + aggregate metrics đơn giản: số buổi Present, số comment, keyword tags nếu có).
- BR-PRG-02: Không dùng AI scoring trong MVP; UI chart đếm buổi / streak / số nhận xét theo tuần-tháng.

## 4. Use Cases

### UC-A Admin
1. Đăng nhập
2. CRUD Facility (Main + Partner)
3. CRUD Teacher account + profile + upload docs
4. CRUD Student + facility assignment + upload medical docs
5. CRUD ClassGroup + gán students
6. Xếp lịch tuần (calendar grid): tạo/sửa/xóa session
7. Clone schedule tuần
8. Xem dashboard lịch toàn hệ thống
9. Cấu hình rate
10. Generate / xem invoice tháng theo student
11. Override attendance nếu cần

### UC-T Teacher
1. Đăng nhập
2. Xem weekly personal schedule
3. Mở session → điểm danh từng HS + bắt buộc comment → submit
4. Xem lịch sử teaching logs của mình
5. Xem (read-only) profile học sinh trong các lớp mình dạy (không sửa hồ sơ)

## 5. Non-Functional Requirements

| ID | Yêu cầu |
|----|---------|
| NFR-01 | API response p95 < 500ms cho list thông thường |
| NFR-02 | Calendar week load < 1s với ~500 sessions |
| NFR-03 | Audit log cho thay đổi schedule, attendance finalize, invoice generate |
| NFR-04 | Backup DB daily |
| NFR-05 | i18n UI mặc định **Tiếng Việt**; code/API English |
| NFR-06 | Responsive: desktop-first (Admin calendar), tablet OK cho Teacher điểm danh |

## 6. Acceptance Criteria (tóm tắt)

- [ ] Admin xếp được lịch dạng tuần, clone tuần thành công
- [ ] Teacher điểm danh + comment bắt buộc khi finalize
- [ ] Invoice tháng = Present × rate đúng priority
- [ ] Student gắn facility; teacher/student docs lưu path/URL
- [ ] Conflict travel/overlap hiện warning trên UI scheduling
