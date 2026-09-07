# 09 — Frontend Spec (Vue 3)

## 1. Stack

- Vue 3 + `<script setup>` + TypeScript
- Vite
- Pinia (auth, schedule filters)
- Vue Router
- Axios + interceptors (JWT refresh)
- UI kit: Element Plus **hoặc** Naive UI
- Calendar: **FullCalendar** (resource/timeGrid) **hoặc** custom CSS Grid
- Charts: Chart.js / ECharts (student progress)
- i18n: vue-i18n (vi default)

## 2. App routes

```
/login
/app                          (layout)
  /dashboard                  Admin overview (optional counts)
  /schedule                   Admin week calendar (system)
  /teachers
  /teachers/:id
  /students
  /students/:id
  /students/:id/progress
  /facilities
  /class-groups
  /billing/rates
  /billing/invoices
  /my/schedule                Teacher week calendar
  /my/sessions/:id/attendance Teacher attendance form
```

Route guards: `requiresAuth`, `roles: ['Admin'] | ['Teacher','Admin']`

## 3. Screen specs

### Login
- Form username/password
- Redirect by role: Admin → `/schedule`, Teacher → `/my/schedule`

### Admin Schedule (critical UX)
- **Week time-block grid** (bắt buộc)
- Toolbar: prev/next week, today, filters (teacher, facility)
- Button **Clone tuần** → dialog chọn source/target
- Click empty slot → drawer/modal Create Session
- Session block shows: class name, teacher short, facility chip
- Conflict icon nếu warning/error từ API
- Color by facility type (Main vs Partner)

### Teacher My Schedule
- Same grid, read-only
- Click session → đi tới attendance nếu chưa finalize; else xem log

### Attendance form
- Table: Student | Status select | Comment textarea | Mood 1–5
- Buttons: Lưu nháp / Hoàn tất điểm danh
- Validate comment non-empty on finalize (client + server)
- Mobile/tablet friendly (Teacher on-site)

### Student Progress
- KPI cards: present count, attendance rate, avg mood
- Line chart mood
- Comment timeline

### Billing
- Rates table + form scope
- Generate invoice dialog
- Invoice detail table lines

## 4. Folder structure (frontend)

```
apps/web/
  src/
    main.ts
    App.vue
    router/
    stores/
      auth.ts
      schedule.ts
    api/
      client.ts
      auth.ts
      sessions.ts
      students.ts
      ...
    views/
      admin/
      teacher/
      auth/
    components/
      schedule/
        WeekCalendar.vue
        SessionBlock.vue
        CloneWeekDialog.vue
        ConflictAlert.vue
      attendance/
        AttendanceForm.vue
      students/
        ProgressChart.vue
        CommentTimeline.vue
      common/
    types/
    utils/
      datetime.ts
      conflicts.ts
```

## 5. UX rules

- Desktop-first cho Admin calendar (min width ~1200px comfortable)
- Teacher attendance usable từ 768px
- Tiếng Việt labels
- Confirm dialogs cho Cancel session, Void invoice, Finalize attendance
- Toast success/error
- Loading skeletons trên calendar fetch

## 6. State & caching

- Fetch sessions khi `weekStart` / filters đổi
- Optimistic UI không bắt buộc; đợi API OK rồi refresh week
- Keep auth user in Pinia + localStorage token

## 7. Accessibility / i18n

- Form labels rõ
- Time format `HH:mm`, date `dd/MM/yyyy`
- Timezone display: Asia/Ho_Chi_Minh
