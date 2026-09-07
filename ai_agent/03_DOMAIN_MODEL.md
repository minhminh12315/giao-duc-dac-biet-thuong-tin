# 03 — Domain Model

## 1. Entity Overview

```
User ─────────────┬──────── TeacherProfile
                  └──────── (Admin không cần profile riêng)

Facility (MainCenter | PartnerSchool)
Student ── FacilityId (current)
StudentDocument
TeacherDocument

ClassGroup
ClassGroupStudent (M:N)

Session (scheduled time block)
SessionStudent (students in session; có thể derive từ ClassGroup + overrides)

AttendanceRecord (per SessionStudent)
TeachingComment (1:1 hoặc gắn AttendanceRecord — bắt buộc khi Present/Absent finalize)

ServiceType / RatePlan
TuitionRate (polymorphic target: Student | ClassGroup | ServiceType | Default)
Invoice
InvoiceLine

ScheduleCloneJob (optional audit of clone ops)
AuditLog
```

## 2. Core Entities (fields)

### User
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | PK |
| UserName | string | |
| Email | string | |
| PasswordHash | string | Identity |
| Role | enum | Admin, Teacher |
| IsActive | bool | |
| TeacherProfileId | uuid? | nếu Teacher |

### TeacherProfile
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| UserId | uuid | 1:1 |
| FullName | string | |
| Phone | string? | |
| DateOfBirth | date? | |
| Address | string? | |
| Specialization | string? | |
| HireDate | date? | |
| Notes | text? | |
| CreatedAt / UpdatedAt | timestamptz | |

### TeacherDocument
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| TeacherProfileId | uuid | |
| DocumentType | enum | Contract, Certificate, Other |
| FileName | string | original |
| ContentType | string | |
| StoragePath | string | relative path or object key |
| PublicUrl | string? | nếu dùng CDN |
| SizeBytes | long | |
| UploadedAt | timestamptz | |
| UploadedByUserId | uuid | |

### Facility
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| Name | string | |
| Type | enum | MainCenter, PartnerSchool |
| Address | string? | |
| ContactPhone | string? | |
| TravelBufferMinutesOverride | int? | null = dùng global MinTravelMinutes |
| IsActive | bool | |

### Student
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| FullName | string | |
| DateOfBirth | date? | |
| Gender | enum? | |
| FacilityId | uuid | **bắt buộc** |
| GuardianName | string? | |
| GuardianPhone | string? | |
| MedicalNotes | text? | |
| Status | enum | Active, Inactive, Graduated |
| CreatedAt / UpdatedAt | | |

### StudentDocument
Tương tự TeacherDocument; `DocumentType`: MedicalReport, Assessment, Other.

### ClassGroup
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| Name | string | ví dụ "Lớp A" |
| FacilityId | uuid? | default location hint |
| ServiceType | enum | AtCenter, AtPartnerSchool |
| IsActive | bool | |

### ClassGroupStudent
| ClassGroupId | StudentId | JoinedAt | LeftAt? |

### Session
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| ClassGroupId | uuid? | optional nếu ad-hoc |
| TeacherProfileId | uuid | |
| FacilityId | uuid | **nơi dạy thực tế** |
| StartAt | timestamptz | |
| EndAt | timestamptz | EndAt > StartAt |
| Title | string? | |
| Status | enum | Scheduled, Completed, Cancelled |
| AttendanceFinalizedAt | timestamptz? | |
| Notes | text? | admin notes |
| SourceCloneSessionId | uuid? | trace clone |

**Invariant:** không cho EndAt ≤ StartAt; Cancelled không tính phí / không bắt điểm danh.

### SessionStudent
| SessionId | StudentId | IsActive |

Khi tạo Session từ ClassGroup: snapshot danh sách HS hiện tại của class vào SessionStudent (để attendance ổn định nếu roster đổi sau).

### AttendanceRecord
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| SessionId | uuid | |
| StudentId | uuid | unique (SessionId, StudentId) |
| Status | enum | Present, Absent, Excused, NotMarked |
| MarkedAt | timestamptz? | |
| MarkedByUserId | uuid? | |

### TeachingComment
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| AttendanceRecordId | uuid | 1:1 |
| Content | text | required khi finalize |
| MoodOrLevel | int? | optional 1–5 scale cho chart |
| Tags | string[]? | jsonb |
| CreatedAt | | |

### TuitionRate
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| Scope | enum | Student, ClassGroup, ServiceType, SystemDefault |
| StudentId | uuid? | |
| ClassGroupId | uuid? | |
| ServiceType | enum? | |
| Amount | decimal(18,2) | VND |
| Currency | string | VND |
| EffectiveFrom | date | |
| EffectiveTo | date? | null = open |
| IsActive | bool | |

### Invoice
| Field | Type | Notes |
|-------|------|-------|
| Id | uuid | |
| StudentId | uuid | |
| PeriodYear | int | |
| PeriodMonth | int | 1–12 |
| TotalAmount | decimal | |
| Status | enum | Draft, Issued, Void |
| GeneratedAt | | |
| Version | int | tăng khi regenerate |

### InvoiceLine
| InvoiceId | SessionId | AttendanceRecordId | UnitRate | Amount | Description |

### AuditLog
| Id | ActorUserId | Action | EntityType | EntityId | BeforeJson | AfterJson | At |

## 3. Relationships (ER text)

```
Facility 1──* Student
Facility 1──* Session
Facility 1──* ClassGroup

User 1──1 TeacherProfile
TeacherProfile 1──* TeacherDocument
TeacherProfile 1──* Session

Student 1──* StudentDocument
Student *──* ClassGroup (via ClassGroupStudent)
Student *──* Session (via SessionStudent)
Student 1──* Invoice
Student 1──* TuitionRate (scoped)

Session 1──* SessionStudent
Session 1──* AttendanceRecord
AttendanceRecord 1──1 TeachingComment
Session *──1 TeacherProfile
Session *──1 Facility

ClassGroup 1──* TuitionRate (scoped)
```

## 4. Domain Invariants (enforce in Domain/Application)

1. Session.FacilityId phải tồn tại và IsActive.
2. Teacher không thể finalize attendance nếu thiếu comment bất kỳ student nào (khi RequireCommentOnFinalize).
3. Chỉ Present tạo InvoiceLine.
4. Rate resolve phải chọn rate Effective trong khoảng ngày session.
5. Clone week: tạo Session mới + SessionStudent copy; **không** copy Attendance.
6. Soft-deleted entities không xuất hiện query mặc định.

## 5. State machines

### Session.Status
`Scheduled` → `Completed` (khi finalize attendance)  
`Scheduled` → `Cancelled`  
`Cancelled` / `Completed` → (Admin reopen optional)

### Invoice.Status
`Draft` → `Issued` → `Void`  
Regenerate: Void old hoặc tăng Version giữ Issued history (khuyến nghị: Version++ new Issued, old → Void).
