# 07 — Business Algorithms

## 1. Rate Resolution

**Input:** `studentId`, `session` (has classGroupId, facility/service context), `sessionDate`

**ServiceType derive:**
- Prefer `ClassGroup.ServiceType` if ClassGroupId present
- Else derive from `Facility.Type`: MainCenter → AtCenter; PartnerSchool → AtPartnerSchool

**Priority (first match wins):**

1. Active `TuitionRate` where `Scope=Student` AND `StudentId` AND date in [EffectiveFrom, EffectiveTo]
2. `Scope=ClassGroup` AND matching ClassGroupId + date
3. `Scope=ServiceType` AND matching ServiceType + date
4. `Scope=SystemDefault` + date
5. Else: throw `RateNotConfiguredException`

Nếu nhiều rate cùng scope trùng khoảng ngày: chọn `EffectiveFrom` mới nhất.

```text
function ResolveRate(student, session, date):
  candidates = rates.active.where(effective covers date)
  return first of:
    candidates.student(student.id)
    candidates.classGroup(session.classGroupId)
    candidates.serviceType(deriveServiceType(session))
    candidates.systemDefault()
```

---

## 2. Dynamic Monthly Billing

**Input:** `studentId`, `year`, `month`, timezone `Asia/Ho_Chi_Minh`

**Steps:**

1. Xác định `[periodStart, periodEnd)` theo timezone.
2. Query `AttendanceRecord` join `Session` where:
   - `student_id = studentId`
   - `status = 'Present'`
   - `session.status != 'Cancelled'`
   - `session.start_at` in period
   - session not soft-deleted
3. For each record:
   - `unitRate = ResolveRate(student, session, session.start_at.date)`
   - line = { sessionId, attendanceId, unitRate, amount: unitRate, description }
4. `total = sum(lines.amount)`
5. Persist Invoice (version = max+1), lines; void previous Issued same period if regenerate policy.

**Pseudo:**

```text
function GenerateInvoice(studentId, year, month):
  presentRecords = loadPresentAttendances(studentId, year, month)
  lines = []
  for r in presentRecords:
    rate = ResolveRate(...)
    lines.add(InvoiceLine(r, rate))
  invoice = Invoice(total=sum(lines), version=next)
  save(invoice, lines)
  return invoice
```

**Không tính:** Absent, Excused, NotMarked, Cancelled sessions.

---

## 3. Schedule Conflict Detection

### 3.1 Overlap (hard conflict for same teacher)

Two sessions A,B of same teacher overlap if:

```text
A.start_at < B.end_at AND B.start_at < A.end_at
AND A.id != B.id
AND neither Cancelled/Deleted
```

Severity: **Error** (default block unless force flag).

### 3.2 Travel time (soft conflict)

Với cùng teacher, sắp xếp sessions trong ngày theo `start_at`.

Với cặp liền kề `(prev, next)`:
- Nếu `prev.facility_id != next.facility_id`
- `gapMinutes = (next.start_at - prev.end_at).TotalMinutes`
- `required = facilityOverride ?? config.MinTravelMinutes`
- Nếu `gapMinutes < required` → Warning `InsufficientTravelTime`

Severity: **Warning** (allow save + show UI badge).

### 3.3 Same facility double-book (optional)

Cùng facility + overlapping time + khác teacher: **Info** only (phòng học vật lý chưa model trong MVP).

### API response shape

```json
{
  "canSave": true,
  "conflicts": [
    {
      "type": "TeacherOverlap",
      "severity": "Error",
      "sessionIdA": "...",
      "sessionIdB": "...",
      "message": "Giáo viên bị trùng lịch 08:30–10:30"
    },
    {
      "type": "InsufficientTravelTime",
      "severity": "Warning",
      "gapMinutes": 15,
      "requiredMinutes": 30,
      "fromFacilityId": "...",
      "toFacilityId": "...",
      "message": "Chỉ còn 15 phút di chuyển giữa hai cơ sở"
    }
  ]
}
```

Save rules:
- Có bất kỳ `Error` → reject trừ `force=true` và `AllowForceSaveWithConflicts`
- Chỉ `Warning` → save OK, return conflicts in response

---

## 4. Clone Week Schedule

**Input:** `sourceWeekStart` (date Monday), `targetWeekStart`, options

**deltaDays = targetWeekStart - sourceWeekStart**

For each source session in `[sourceWeekStart, sourceWeekStart+7d)` where status != Cancelled (unless includeCancelled):

```text
newStart = source.start_at + deltaDays
newEnd   = source.end_at + deltaDays
newSession = copy(teacher, facility, classGroup, title, notes)
            without attendance fields
copy SessionStudents from source
run ConflictDetection against existing target week
if conflict Error and policy=Skip: skip
else create
```

**Không copy:** AttendanceRecord, TeachingComment, AttendanceFinalizedAt.

Return summary:

```json
{
  "created": 42,
  "skipped": 3,
  "warnings": [ ... ]
}
```

---

## 5. Finalize Attendance

```text
function FinalizeAttendance(sessionId, items[{studentId, status, comment, mood?}]):
  assert actor is assigned teacher or admin
  assert session not Cancelled
  assert all sessionStudents covered
  if RequireCommentOnFinalize:
    assert every item has non-whitespace comment
  upsert AttendanceRecords
  upsert TeachingComments
  session.AttendanceFinalizedAt = now
  session.Status = Completed
  write AuditLog
```

---

## 6. Progress Aggregation (Student)

**Metrics MVP:**

| Metric | Formula |
|--------|---------|
| PresentCount | count Present in range |
| AbsentCount | count Absent |
| AttendanceRate | Present / (Present+Absent+Excused) |
| CommentCount | count comments |
| AvgMood | avg(mood_or_level) where not null |
| SeriesMood | points (date, mood) ordered |
| CommentFeed | list content + teacher + session time |

Chart UI: line (mood), bar (present per week).

---

## 7. Session create from ClassGroup

```text
function CreateSession(dto):
  validate times
  conflicts = Detect(teacher, times, facility)
  students = active ClassGroupStudents if classGroupId
  persist Session + SessionStudents
  return session + conflicts
```
