# Hệ thống Quản lý Trung tâm Giáo dục Đặc biệt (SECMS)

Stack: **.NET 8 Web API** · **Vue 3 + TypeScript** · **PostgreSQL 16**

Spec chi tiết: [`ai_agent/`](./ai_agent/00_README.md)

## Chạy nhanh (local)

### 1. Database

```bash
docker compose up -d
```

Postgres map cổng **5433** → 5432 (tránh conflict với Postgres sẵn có).

### 2. Backend API

```bash
cd src/backend
dotnet run --project Secms.Api
```

- API: http://localhost:5080  
- Swagger: http://localhost:5080/swagger  

Lần chạy đầu sẽ migrate + seed dữ liệu demo.

### 3. Frontend

```bash
cd src/frontend
npm install
npm run dev
```

- Web: http://localhost:5173

## Tài khoản demo

| Role | User | Password |
|------|------|----------|
| Admin | `admin` | `Admin@123` |
| Teacher | `gv01` | `Teacher@123` |

Seed kèm: 1 cơ sở chính, 1 trường đối tác, 2 học sinh, 2 lớp, đơn giá, 2 ca học tuần hiện tại (có khoảng cách di chuyển ngắn để demo warning).

## Tính năng đã có

- RBAC Admin / Teacher (JWT)
- Quản lý Cơ sở, Giáo viên, Học sinh, Lớp
- Upload tài liệu (PDF/ảnh) — lưu path trên disk
- Lịch tuần (FullCalendar) + tạo/sửa/hủy ca + clone tuần
- Cảnh báo trùng lịch GV & thiếu thời gian di chuyển giữa 2 cơ sở
- Điểm danh + nhận xét bắt buộc + mood 1–5
- Biểu đồ / timeline tiến trình học sinh
- Đơn giá theo scope + hóa đơn tháng = Present × rate

## Cấu trúc

```
ai_agent/          Spec phân tích hệ thống
src/backend/       Secms.sln (Api / Application / Domain / Infrastructure)
src/frontend/      Vue 3 SPA
docker-compose.yml PostgreSQL
```
