# AI Agent — Hệ thống Quản lý Trung tâm Giáo dục Đặc biệt

> **Project:** Special Education Center Management System (SECMS)  
> **Model:** Hybrid (Cơ sở chính + On-site tại trường đối tác)  
> **Stack:** .NET 8 (API) · Vue 3 (SPA) · PostgreSQL 16  
> **Mục đích thư mục này:** Spec đầy đủ để AI/dev triển khai end-to-end mà không cần hỏi lại nghiệp vụ.

---

## Cách dùng (cho AI Agent)

1. Đọc tuần tự từ `00` → `12`.
2. Mỗi file là **source of truth** cho phạm vi của nó; nếu mâu thuẫn, ưu tiên số cao hơn về chi tiết kỹ thuật, ưu tiên `01` về nghiệp vụ.
3. Triển khai theo `12_IMPLEMENTATION_ROADMAP.md` (phase-by-phase).
4. Không tự ý thêm module ngoài scope (ví dụ: CRM phụ huynh, thanh toán online gateway) trừ khi được yêu cầu.

---

## Mục lục tài liệu

| # | File | Nội dung |
|---|------|----------|
| 00 | `00_README.md` | Tổng quan & cách dùng |
| 01 | `01_BUSINESS_REQUIREMENTS.md` | Yêu cầu nghiệp vụ, actors, use cases |
| 02 | `02_ARCHITECTURE.md` | Kiến trúc hệ thống, deployment, patterns |
| 03 | `03_DOMAIN_MODEL.md` | Entities, relationships, invariants |
| 04 | `04_DATABASE_SCHEMA.md` | PostgreSQL DDL, indexes, constraints |
| 05 | `05_RBAC_PERMISSIONS.md` | Roles, permissions matrix |
| 06 | `06_MODULES_SPEC.md` | Spec chi tiết từng module |
| 07 | `07_BUSINESS_ALGORITHMS.md` | Billing, clone schedule, conflict detection |
| 08 | `08_API_CONTRACTS.md` | REST API endpoints, DTOs |
| 09 | `09_FRONTEND_SPEC.md` | Vue 3 structure, screens, calendar UX |
| 10 | `10_FILE_STORAGE.md` | Upload documents (ảnh/PDF), paths |
| 11 | `11_PROJECT_STRUCTURE.md` | Folder structure .NET + Vue |
| 12 | `12_IMPLEMENTATION_ROADMAP.md` | Phases, checklist, Definition of Done |
| 13 | `13_AGENT_PROMPT.md` | Prompt copy-paste + checklist MVP |
| 14 | `14_ENUMS_GLOSSARY.md` | Enums, nhãn VI, thuật ngữ |

---

## Phạm vi MVP (In Scope)

- Auth + RBAC (Admin, Teacher)
- Teacher Management (profile, documents, weekly schedule, attendance + comments)
- Student Management (profile, facility assignment, attendance history, progress from comments)
- Scheduling (calendar week grid, assign teacher/class/facility, clone week)
- Billing (rate config per student/class/service type, dynamic tuition from attendance)

## Out of Scope (MVP)

- Parent/Guardian portal
- Online payment gateway
- Mobile native apps
- Multi-tenant SaaS
- AI scoring of student development (chỉ aggregate comments → chart/report)

---

## Thuật ngữ nhanh

| Term | Nghĩa |
|------|-------|
| Facility | Cơ sở chính **hoặc** Trường đối tác |
| Session / Slot | Một ca học (time block) trên lịch |
| ClassGroup | Lớp / nhóm học sinh trong một ca |
| AttendanceStatus | Present / Absent / Excused |
| Rate | Đơn giá học phí gắn student hoặc class hoặc service type |
| TeachingLog | Điểm danh + nhận xét từng học sinh trong 1 ca |
