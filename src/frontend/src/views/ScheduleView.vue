<template>
  <div class="page schedule-page">
    <header class="sched-hero">
      <div class="sched-hero-copy">
        <p class="sched-kicker">{{ editable ? 'Vận hành' : 'Cá nhân' }}</p>
        <h1>{{ editable ? 'Lịch hệ thống' : 'Lịch của tôi' }}</h1>
        <p class="sched-lead">
          {{
            editable
              ? 'Time-block tuần · cảnh báo trùng lịch và thời gian di chuyển giữa cơ sở.'
              : 'Chạm vào ca để điểm danh và ghi nhận xét học sinh.'
          }}
        </p>
      </div>
      <div v-if="editable" class="sched-hero-actions">
        <el-button @click="cloneVisible = true">Clone tuần</el-button>
        <el-button type="primary" @click="openCreate()">Thêm ca</el-button>
      </div>
    </header>

    <section class="sched-chrome" aria-label="Điều khiển lịch">
      <div class="week-nav">
        <button type="button" class="icon-btn" aria-label="Tuần trước" @click="shiftWeek(-1)">‹</button>
        <button type="button" class="today-btn" @click="goToday">Hôm nay</button>
        <button type="button" class="icon-btn" aria-label="Tuần sau" @click="shiftWeek(1)">›</button>
        <div class="week-label">
          <strong>{{ weekLabel }}</strong>
          <span>{{ sessions.length }} ca trong tuần</span>
        </div>
      </div>

      <div v-if="editable" class="filters">
        <el-select v-model="teacherFilter" clearable placeholder="Giáo viên" style="width:180px" @change="reload">
          <el-option v-for="t in teachers" :key="t.id" :label="t.fullName" :value="t.id" />
        </el-select>
        <el-select v-model="facilityFilter" clearable placeholder="Cơ sở" style="width:180px" @change="reload">
          <el-option v-for="f in facilities" :key="f.id" :label="f.name" :value="f.id" />
        </el-select>
        <div class="legend">
          <span><i class="dot ink" /> Cơ sở chính</span>
          <span><i class="dot accent" /> Trường đối tác</span>
        </div>
      </div>
    </section>

    <div class="calendar-shell" @mousedown="onGridMouseDown">
      <div class="cal-grid" :style="gridStyle">
        <div class="corner" />
        <div
          v-for="day in days"
          :key="day.key"
          class="day-head"
          :class="{ today: day.isToday }"
        >
          <span class="dow">{{ day.dow }}</span>
          <strong>{{ day.dateLabel }}</strong>
        </div>

        <template v-for="hour in hours" :key="hour">
          <div class="hour-label">{{ String(hour).padStart(2, '0') }}:00</div>
          <div
            v-for="day in days"
            :key="`${day.key}-${hour}`"
            class="slot"
            :class="{ today: day.isToday }"
            :data-day="day.key"
            :data-hour="hour"
            @dblclick="editable && openCreate(slotStart(day.date, hour), slotStart(day.date, hour + 2))"
          />
        </template>

        <div
          v-for="block in eventBlocks"
          :key="block.id"
          class="event-block"
          :class="block.partner ? 'partner' : 'main'"
          :style="block.style"
          @click.stop="onEventClick(block.session)"
          @mousedown.stop
        >
          <strong>{{ block.title }}</strong>
          <span>{{ block.timeLabel }} · {{ block.teacher }}</span>
        </div>

        <div v-if="draft" class="event-block draft" :style="draft.style" />
      </div>
    </div>

    <el-drawer v-model="drawer" :title="form.id ? 'Sửa ca học' : 'Tạo ca học'" size="420px" destroy-on-close>
      <el-form label-position="top" @submit.prevent>
        <el-form-item label="Tiêu đề"><el-input v-model="form.title" /></el-form-item>
        <el-form-item label="Giáo viên" required>
          <el-select v-model="form.teacherProfileId" filterable style="width:100%">
            <el-option v-for="t in teachers" :key="t.id" :label="t.fullName" :value="t.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Cơ sở" required>
          <el-select v-model="form.facilityId" filterable style="width:100%">
            <el-option
              v-for="f in facilities"
              :key="f.id"
              :label="`${f.name} (${f.type === 'MainCenter' ? 'Chính' : 'Đối tác'})`"
              :value="f.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="Lớp">
          <el-select v-model="form.classGroupId" clearable style="width:100%" @change="onClassChange">
            <el-option v-for="c in classes" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Bắt đầu">
          <el-date-picker v-model="form.startAt" type="datetime" style="width:100%" />
        </el-form-item>
        <el-form-item label="Kết thúc">
          <el-date-picker v-model="form.endAt" type="datetime" style="width:100%" />
        </el-form-item>
        <el-form-item label="Ghi chú"><el-input v-model="form.notes" type="textarea" /></el-form-item>
        <el-alert
          v-if="conflicts.length"
          :title="conflicts.map((c) => c.message).join(' · ')"
          :type="conflicts.some((c) => c.severity === 'Error') ? 'error' : 'warning'"
          show-icon
          :closable="false"
          style="margin-bottom: 12px"
        />
        <div class="drawer-actions">
          <el-button v-if="form.id" plain :disabled="saving" @click="cancelSession">Hủy ca</el-button>
          <el-button :disabled="saving" @click="drawer = false">Đóng</el-button>
          <el-button type="primary" :loading="saving" :disabled="!canSave" @click="save(false)">Lưu</el-button>
          <el-button
            v-if="conflicts.some((c) => c.severity === 'Error')"
            type="warning"
            :loading="saving"
            @click="save(true)"
          >
            Force lưu
          </el-button>
        </div>
      </el-form>
    </el-drawer>

    <el-dialog v-model="cloneVisible" title="Clone lịch tuần" width="480px" destroy-on-close>
      <el-form label-position="top" @submit.prevent>
        <el-form-item label="Tuần nguồn (Thứ 2)">
          <el-date-picker v-model="cloneForm.source" type="date" style="width:100%" />
        </el-form-item>
        <el-form-item label="Tuần đích (Thứ 2)">
          <el-date-picker v-model="cloneForm.target" type="date" style="width:100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <div class="dialog-actions">
          <el-button @click="cloneVisible = false" :disabled="cloning">Hủy</el-button>
          <el-button type="primary" :loading="cloning" @click="doClone">Clone</el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { onBeforeRouteLeave, useRouter } from 'vue-router'
import dayjs, { type Dayjs } from 'dayjs'
import 'dayjs/locale/vi'
import updateLocale from 'dayjs/plugin/updateLocale'
import { ElMessage, ElMessageBox } from 'element-plus'
import api from '../api/client'
import type { ClassGroup, Facility, ScheduleConflict, Session, Teacher } from '../types'

dayjs.extend(updateLocale)
dayjs.updateLocale('vi', { weekStart: 1 })
dayjs.locale('vi')

const DEMO_TAG = '#demo-week'
const DAY_START = 7
const DAY_END = 18
const HOUR_PX = 56

const props = defineProps<{ mode?: 'admin' | 'teacher' }>()
const editable = computed(() => props.mode !== 'teacher')

const router = useRouter()
const teachers = ref<Teacher[]>([])
const facilities = ref<Facility[]>([])
const classes = ref<ClassGroup[]>([])
const sessions = ref<Session[]>([])
const teacherFilter = ref<string>()
const facilityFilter = ref<string>()
const drawer = ref(false)
const saving = ref(false)
const conflicts = ref<ScheduleConflict[]>([])
const cloneVisible = ref(false)
const cloning = ref(false)
const cloneForm = reactive({ source: new Date(), target: dayjs().add(7, 'day').toDate() })
const weekStart = ref(dayjs().startOf('week'))
let reloadSeq = 0

const form = reactive({
  id: '' as string,
  title: '',
  teacherProfileId: '',
  facilityId: '',
  classGroupId: '' as string | undefined,
  startAt: new Date() as Date | null,
  endAt: dayjs().add(2, 'hour').toDate() as Date | null,
  notes: '',
  studentIds: [] as string[],
  keepDemoTag: false,
})

const draft = ref<{ style: Record<string, string> } | null>(null)
let drag: { day: Dayjs; startHour: number; endHour: number } | null = null

const canSave = computed(
  () => !!form.teacherProfileId && !!form.facilityId && !!form.startAt && !!form.endAt,
)

const hours = computed(() => {
  const list: number[] = []
  for (let h = DAY_START; h < DAY_END; h++) list.push(h)
  return list
})

const days = computed(() => {
  const today = dayjs().format('YYYY-MM-DD')
  return Array.from({ length: 7 }, (_, i) => {
    const d = weekStart.value.add(i, 'day')
    return {
      key: d.format('YYYY-MM-DD'),
      date: d,
      dow: d.format('dd'),
      dateLabel: d.format('D/M'),
      isToday: d.format('YYYY-MM-DD') === today,
    }
  })
})

const weekLabel = computed(() => {
  const mon = weekStart.value
  const sun = mon.add(6, 'day')
  if (mon.month() === sun.month()) return `${mon.format('D')} – ${sun.format('D MMMM YYYY')}`
  return `${mon.format('D MMM')} – ${sun.format('D MMM YYYY')}`
})

const gridStyle = computed(() => ({
  '--cols': '7',
  '--hour-px': `${HOUR_PX}px`,
  '--rows': String(hours.value.length),
}))

type EventBlock = {
  id: string
  session: Session
  title: string
  teacher: string
  timeLabel: string
  partner: boolean
  style: Record<string, string>
}

const eventBlocks = computed<EventBlock[]>(() => {
  const mon = weekStart.value.startOf('day')
  const sun = mon.add(7, 'day')
  return sessions.value
    .map((s) => {
      const start = dayjs(s.startAt)
      const end = dayjs(s.endAt)
      if (end.isBefore(mon) || !start.isBefore(sun)) return null

      const dayIndex = start.startOf('day').diff(mon, 'day')
      if (dayIndex < 0 || dayIndex > 6) return null

      const startMin = start.hour() * 60 + start.minute()
      const endMin = end.hour() * 60 + end.minute()
      const gridStart = DAY_START * 60
      const gridEnd = DAY_END * 60
      const clampedStart = Math.max(startMin, gridStart)
      const clampedEnd = Math.min(endMin, gridEnd)
      if (clampedEnd <= clampedStart) return null

      const top = ((clampedStart - gridStart) / 60) * HOUR_PX
      const height = Math.max(((clampedEnd - clampedStart) / 60) * HOUR_PX - 4, 28)

      return {
        id: s.id,
        session: s,
        title: s.title || s.classGroupName || 'Ca học',
        teacher: s.teacherName,
        timeLabel: `${start.format('HH:mm')}–${end.format('HH:mm')}`,
        partner: s.facilityType === 'PartnerSchool',
        style: {
          left: `calc(72px + (100% - 72px) * ${dayIndex} / 7 + 4px)`,
          width: `calc((100% - 72px) / 7 - 8px)`,
          top: `calc(52px + ${top}px)`,
          height: `${height}px`,
        },
      }
    })
    .filter(Boolean) as EventBlock[]
})

function displayNotes(raw?: string | null) {
  if (!raw || raw === DEMO_TAG) return ''
  return raw
}

function slotStart(day: Dayjs, hour: number) {
  return day.hour(hour).minute(0).second(0).millisecond(0).toDate()
}

function shiftWeek(delta: number) {
  weekStart.value = weekStart.value.add(delta, 'week')
}

function goToday() {
  weekStart.value = dayjs().startOf('week')
}

async function reload() {
  const from = weekStart.value.startOf('day').toISOString()
  const to = weekStart.value.add(7, 'day').startOf('day').toISOString()
  const seq = ++reloadSeq
  try {
    const params: Record<string, string | undefined> = {
      from,
      to,
      teacherId: teacherFilter.value || undefined,
      facilityId: facilityFilter.value || undefined,
    }
    if (!editable.value) {
      const auth = (await import('../stores/auth')).useAuthStore()
      if (auth.user?.teacherProfileId) {
        params.teacherId = auth.user.teacherProfileId
      }
    }
    const { data } = await api.get<Session[]>('/sessions', { params })
    if (seq !== reloadSeq) return
    sessions.value = data.filter((s) => s.status !== 'Cancelled')
  } catch (e: any) {
    if (seq !== reloadSeq) return
    ElMessage.error(e.message || 'Không tải được lịch')
  }
}

watch(weekStart, () => {
  void reload()
}, { immediate: true })

function hitSlot(e: MouseEvent) {
  const el = (e.target as HTMLElement).closest('.slot') as HTMLElement | null
  if (!el) return null
  const dayKey = el.dataset.day
  const hour = Number(el.dataset.hour)
  const day = days.value.find((d) => d.key === dayKey)?.date
  if (!day || Number.isNaN(hour)) return null
  return { day, hour }
}

function draftStyle(dayIndex: number, startHour: number, endHour: number) {
  const a = Math.min(startHour, endHour)
  const b = Math.max(startHour, endHour) + 1
  const top = (a - DAY_START) * HOUR_PX
  const height = Math.max((b - a) * HOUR_PX - 4, 28)
  return {
    left: `calc(72px + (100% - 72px) * ${dayIndex} / 7 + 4px)`,
    width: `calc((100% - 72px) / 7 - 8px)`,
    top: `calc(52px + ${top}px)`,
    height: `${height}px`,
  }
}

function onGridMouseDown(e: MouseEvent) {
  if (!editable.value || e.button !== 0) return
  const hit = hitSlot(e)
  if (!hit) return
  e.preventDefault()
  drag = { day: hit.day, startHour: hit.hour, endHour: hit.hour }
  const dayIndex = hit.day.diff(weekStart.value, 'day')
  draft.value = { style: draftStyle(dayIndex, hit.hour, hit.hour) }

  const onMove = (ev: MouseEvent) => {
    if (!drag) return
    const h = hitSlot(ev)
    if (!h || !h.day.isSame(drag.day, 'day')) return
    drag.endHour = h.hour
    draft.value = { style: draftStyle(dayIndex, drag.startHour, drag.endHour) }
  }
  const onUp = () => {
    window.removeEventListener('mousemove', onMove)
    window.removeEventListener('mouseup', onUp)
    if (!drag) return
    const startH = Math.min(drag.startHour, drag.endHour)
    const endH = Math.max(drag.startHour, drag.endHour) + 1
    const start = slotStart(drag.day, startH)
    const end = slotStart(drag.day, Math.min(endH, DAY_END))
    drag = null
    draft.value = null
    if (dayjs(end).diff(start, 'minute') < 30) return
    openCreate(start, end)
  }
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}

function onEventClick(s: Session) {
  if (!editable.value) {
    router.push(`/my/sessions/${s.id}/attendance`)
    return
  }
  form.id = s.id
  form.title = s.title || ''
  form.teacherProfileId = s.teacherProfileId
  form.facilityId = s.facilityId
  form.classGroupId = s.classGroupId || undefined
  form.startAt = new Date(s.startAt)
  form.endAt = new Date(s.endAt)
  form.keepDemoTag = s.notes === DEMO_TAG
  form.notes = displayNotes(s.notes)
  form.studentIds = s.studentIds
  conflicts.value = []
  drawer.value = true
}

function openCreate(start?: Date, end?: Date) {
  form.id = ''
  form.title = ''
  form.teacherProfileId = teachers.value[0]?.id || ''
  form.facilityId = facilities.value[0]?.id || ''
  form.classGroupId = undefined
  form.startAt = start || new Date()
  form.endAt = end || dayjs(form.startAt).add(2, 'hour').toDate()
  form.notes = ''
  form.keepDemoTag = false
  form.studentIds = []
  conflicts.value = []
  drawer.value = true
}

function onClassChange(id?: string) {
  if (!id) {
    form.studentIds = []
    return
  }
  const c = classes.value.find((x) => x.id === id)
  if (c) {
    form.studentIds = [...c.studentIds]
    if (c.facilityId) form.facilityId = c.facilityId
    if (!form.title) form.title = c.name
  }
}

async function save(force: boolean) {
  if (!canSave.value || !form.startAt || !form.endAt) {
    ElMessage.warning('Vui lòng chọn giáo viên, cơ sở và thời gian')
    return
  }
  if (form.endAt <= form.startAt) {
    ElMessage.warning('Thời gian kết thúc phải sau thời gian bắt đầu')
    return
  }
  saving.value = true
  try {
    const notes = form.notes.trim() || (form.keepDemoTag ? DEMO_TAG : '')
    const payload = {
      classGroupId: form.classGroupId || null,
      teacherProfileId: form.teacherProfileId,
      facilityId: form.facilityId,
      startAt: form.startAt.toISOString(),
      endAt: form.endAt.toISOString(),
      title: form.title,
      notes,
      studentIds: form.studentIds,
      force,
    }
    let conflictsFromApi: ScheduleConflict[] = []
    if (form.id) {
      const { data } = await api.put(`/sessions/${form.id}`, payload)
      conflictsFromApi = data.conflicts || []
      conflicts.value = conflictsFromApi
    } else {
      const { data } = await api.post('/sessions', payload)
      conflictsFromApi = data.conflicts || []
      conflicts.value = conflictsFromApi
      form.id = data.session.id
    }
    if (conflictsFromApi.length && !force) {
      ElMessage.warning(conflictsFromApi.map((c) => c.message).join(' · '))
      await reload()
      return
    }
    ElMessage.success('Đã lưu ca học')
    drawer.value = false
    await reload()
  } catch (e: any) {
    if (e.conflicts) {
      conflicts.value = e.conflicts
      ElMessage.warning(e.message || 'Có xung đột lịch')
    } else {
      ElMessage.error(e.message || 'Không lưu được ca')
    }
  } finally {
    saving.value = false
  }
}

async function cancelSession() {
  try {
    await ElMessageBox.confirm('Hủy ca học này?', 'Xác nhận', {
      confirmButtonText: 'Hủy ca',
      cancelButtonText: 'Đóng',
      type: 'warning',
    })
  } catch {
    return
  }
  saving.value = true
  try {
    await api.delete(`/sessions/${form.id}`)
    ElMessage.success('Đã hủy ca')
    drawer.value = false
    await reload()
  } catch (e: any) {
    ElMessage.error(e.message || 'Không hủy được ca')
  } finally {
    saving.value = false
  }
}

async function doClone() {
  if (!cloneForm.source || !cloneForm.target) {
    ElMessage.warning('Chọn tuần nguồn và tuần đích')
    return
  }
  cloning.value = true
  try {
    const { data } = await api.post('/schedules/clone-week', {
      sourceWeekStart: dayjs(cloneForm.source).format('YYYY-MM-DD'),
      targetWeekStart: dayjs(cloneForm.target).format('YYYY-MM-DD'),
      skipOnError: true,
    })
    ElMessage.success(`Đã tạo ${data.created} ca, bỏ qua ${data.skipped}`)
    cloneVisible.value = false
    await reload()
  } catch (e: any) {
    ElMessage.error(e.message || 'Clone thất bại')
  } finally {
    cloning.value = false
  }
}

onMounted(async () => {
  try {
    if (editable.value) {
      const [t, f, c] = await Promise.all([
        api.get<Teacher[]>('/teachers'),
        api.get<Facility[]>('/facilities'),
        api.get<ClassGroup[]>('/class-groups'),
      ])
      teachers.value = t.data
      facilities.value = f.data
      classes.value = c.data
    } else {
      facilities.value = (await api.get<Facility[]>('/facilities')).data
    }
  } catch (e: any) {
    ElMessage.error(e.message || 'Không tải được dữ liệu lịch')
  }
})

onBeforeRouteLeave(() => {
  drawer.value = false
  cloneVisible.value = false
  draft.value = null
})

onBeforeUnmount(() => {
  draft.value = null
})
</script>

<style scoped>
.schedule-page {
  gap: 20px;
}

.sched-hero {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 24px;
  flex-wrap: wrap;
}

.sched-kicker {
  margin: 0 0 8px;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--text-muted);
}

.sched-hero h1 {
  margin: 0;
  font-size: 40px;
  font-weight: 650;
  line-height: 1.05;
  letter-spacing: -0.02em;
}

.sched-lead {
  margin: 10px 0 0;
  max-width: 42ch;
  color: var(--text-muted);
  font-size: 14px;
  line-height: 1.45;
}

.sched-hero-actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.sched-chrome {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 16px;
  background: var(--canvas-soft);
  border-radius: 24px;
}

.week-nav {
  display: flex;
  align-items: center;
  gap: 8px;
}

.icon-btn,
.today-btn {
  appearance: none;
  border: 1px solid var(--hairline);
  background: var(--canvas);
  color: var(--ink);
  font: inherit;
  font-weight: 600;
  cursor: pointer;
  border-radius: 999px;
  height: 36px;
}

.icon-btn {
  width: 36px;
  font-size: 20px;
  line-height: 1;
}

.today-btn {
  padding: 0 14px;
  font-size: 13px;
}

.icon-btn:hover,
.today-btn:hover {
  background: var(--ink);
  color: #fff;
  border-color: var(--ink);
}

.week-label {
  margin-left: 8px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.week-label strong {
  font-size: 15px;
  font-weight: 650;
}

.week-label span {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
}

.filters {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}

.legend {
  display: flex;
  gap: 14px;
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 600;
}

.legend span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}

.dot.ink { background: #141414; }
.dot.accent { background: #0066ff; }

.calendar-shell {
  position: relative;
  z-index: 0;
  overflow: auto;
  border: 1px solid var(--hairline);
  border-radius: 24px;
  background: var(--canvas);
  user-select: none;
}

.cal-grid {
  position: relative;
  display: grid;
  grid-template-columns: 72px repeat(7, minmax(0, 1fr));
  grid-template-rows: 52px repeat(var(--rows), var(--hour-px));
  min-width: 860px;
}

.corner {
  position: sticky;
  left: 0;
  z-index: 3;
  background: var(--canvas-soft);
  border-bottom: 1px solid var(--hairline);
  border-right: 1px solid var(--hairline);
}

.day-head {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  background: var(--canvas-soft);
  border-bottom: 1px solid var(--hairline);
  border-right: 1px solid var(--hairline);
  font-size: 12px;
}

.day-head .dow {
  color: var(--text-muted);
  font-weight: 600;
  text-transform: capitalize;
}

.day-head strong {
  font-size: 14px;
  font-weight: 650;
}

.day-head.today strong {
  color: var(--accent);
}

.hour-label {
  position: sticky;
  left: 0;
  z-index: 2;
  display: flex;
  align-items: flex-start;
  justify-content: flex-end;
  padding: 6px 10px 0 0;
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted);
  background: var(--canvas);
  border-right: 1px solid var(--hairline);
  border-bottom: 1px solid var(--hairline-soft);
}

.slot {
  border-right: 1px solid var(--hairline);
  border-bottom: 1px solid var(--hairline-soft);
  cursor: crosshair;
}

.slot.today {
  background: rgba(0, 102, 255, 0.03);
}

.event-block {
  position: absolute;
  z-index: 4;
  border-radius: 12px;
  padding: 6px 8px;
  color: #fff;
  overflow: hidden;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.event-block.main { background: #141414; }
.event-block.partner { background: #0066ff; }
.event-block.draft {
  background: rgba(0, 102, 255, 0.22);
  border: 1px dashed var(--accent);
  pointer-events: none;
}

.event-block strong {
  font-size: 12px;
  font-weight: 650;
  line-height: 1.25;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.event-block span {
  font-size: 10px;
  font-weight: 500;
  opacity: 0.88;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

@media (max-width: 960px) {
  .sched-hero h1 { font-size: 32px; }
}
</style>
