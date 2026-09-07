# AI Agent Prompt — Triển khai từ Spec

Copy block dưới đây làm system/user prompt khi bắt đầu code:

---

Bạn là Senior Full-stack (.NET 8 + Vue 3 + PostgreSQL).  
Hãy triển khai **Hệ thống Quản lý Trung tâm Giáo dục Đặc biệt (Hybrid)** theo đúng spec trong thư mục `ai_agent/`.

**Bắt buộc đọc trước khi code:**
1. `ai_agent/00_README.md`
2. `ai_agent/01_BUSINESS_REQUIREMENTS.md`
3. `ai_agent/03_DOMAIN_MODEL.md`
4. `ai_agent/04_DATABASE_SCHEMA.md`
5. `ai_agent/07_BUSINESS_ALGORITHMS.md`
6. `ai_agent/12_IMPLEMENTATION_ROADMAP.md`

**Stack cố định:** ASP.NET Core 8 Web API, EF Core + PostgreSQL, Vue 3 + TS + Vite, JWT RBAC (Admin/Teacher).

**Ràng buộc nghiệp vụ không được bỏ:**
- Student bắt buộc gắn Facility (MainCenter | PartnerSchool)
- Documents lưu path/URL, không lưu binary trong DB
- Scheduling UI dạng tuần calendar/time-block; có clone tuần
- Conflict: overlap teacher (error) + thiếu thời gian di chuyển giữa 2 cơ sở (warning)
- Attendance finalize bắt buộc comment từng học sinh
- Học phí tháng = số ca Present × rate (priority Student > ClassGroup > ServiceType > Default); nghỉ không tính phí
- Comments/mood là nguồn Student Progress

**Cách làm:** Implement theo Phase 0 → 5 trong roadmap. Mỗi phase xong phải thỏa DoD. Không thêm Parent portal / payment gateway.

**Output:** Code trong `src/backend` và `src/frontend` đúng `11_PROJECT_STRUCTURE.md`, kèm README chạy local + docker-compose.

---

## Checklist trước khi coi là xong MVP

- [ ] Login Admin & Teacher
- [ ] CRUD Facility / Teacher / Student / ClassGroup / Docs
- [ ] Calendar tuần Admin + clone
- [ ] Warning trùng lịch & travel time
- [ ] Teacher điểm danh + comment
- [ ] Progress chart từ comments/mood
- [ ] Rate config + generate invoice đúng công thức
- [ ] Tests P0 trong roadmap
