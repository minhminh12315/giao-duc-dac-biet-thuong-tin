# 05 — RBAC & Permissions

## 1. Roles

| Role | Code | Mô tả |
|------|------|--------|
| Admin | `Admin` | Full system |
| Teacher | `Teacher` | Personal schedule + attendance |

MVP dùng **Role-based**; có thể mở rộng Permission claims sau.

## 2. Permission Matrix

| Resource / Action | Admin | Teacher |
|-------------------|:-----:|:-------:|
| Users CRUD | ✅ | ❌ |
| Facilities CRUD | ✅ | ❌ (read nếu cần tên cơ sở trên lịch) |
| Teachers CRUD + docs | ✅ | ❌ (self profile read; self docs read) |
| Students CRUD + docs | ✅ | Read students in assigned sessions |
| ClassGroups CRUD | ✅ | Read own classes |
| Sessions CRUD (system) | ✅ | ❌ |
| View own week schedule | ✅ | ✅ |
| View all schedules dashboard | ✅ | ❌ |
| Clone week | ✅ | ❌ |
| Attendance draft/finalize own session | ✅ | ✅ (own sessions only) |
| Attendance edit any | ✅ | ❌ (trừ trong edit window own) |
| Teaching comments write | ✅ | ✅ own |
| Progress report student | ✅ | ✅ students they taught |
| Tuition rates CRUD | ✅ | ❌ |
| Invoice generate/view | ✅ | ❌ |
| Audit logs | ✅ | ❌ |

## 3. Policy examples (.NET)

```csharp
options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
options.AddPolicy("TeacherOrAdmin", p => p.RequireRole("Admin", "Teacher"));
```

Resource-based:

```csharp
// Teacher may only finalize Session if session.TeacherProfile.UserId == currentUserId
// OR role == Admin
```

## 4. API authorization checklist

| Endpoint group | Auth |
|----------------|------|
| `/api/auth/*` | Anonymous login; refresh authenticated |
| `/api/admin/**` | AdminOnly |
| `/api/teachers/me/**` | TeacherOrAdmin |
| `/api/sessions/{id}/attendance` | Teacher assigned OR Admin |
| `/api/billing/**` | AdminOnly |
| `/api/files/**` | Authenticated + ownership check |

## 5. Account lifecycle

1. Admin tạo User (role Teacher) → tạo TeacherProfile
2. Temp password hoặc invite (MVP: Admin set password)
3. `IsActive=false` → không login
4. Soft-delete teacher: deactivate user + soft-delete profile; sessions lịch sử giữ nguyên

## 6. Data scoping rules

- Teacher calendar query: `WHERE teacher_profile_id = currentTeacherId`
- Teacher student list: distinct students from SessionStudent of their sessions
- Admin: no row filter (trừ soft-delete)
