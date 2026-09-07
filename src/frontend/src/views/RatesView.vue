<template>
  <div class="page">
    <PageHeader title="Đơn giá." subtitle="Ưu tiên: Học sinh → Lớp → Loại dịch vụ → Mặc định.">
      <template #actions>
        <el-button type="primary" @click="open()">Thêm đơn giá</el-button>
      </template>
    </PageHeader>

    <div class="surface">
      <el-table :data="rows" v-loading="loading" empty-text="Chưa cấu hình đơn giá">
        <el-table-column label="Phạm vi" width="160">
          <template #default="{ row }">{{ rateScopeLabel(row.scope) }}</template>
        </el-table-column>
        <el-table-column label="Số tiền" width="160">
          <template #default="{ row }">{{ formatMoney(row.amount) }} {{ row.currency }}</template>
        </el-table-column>
        <el-table-column label="Tham chiếu" min-width="200">
          <template #default="{ row }">
            {{ resolveRef(row) }}
          </template>
        </el-table-column>
        <el-table-column prop="effectiveFrom" label="Hiệu lực từ" width="130" />
        <el-table-column label="Hiệu lực" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'" round>
              {{ row.isActive ? 'Có' : 'Không' }}
            </el-tag>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" title="Thêm đơn giá" width="480px" destroy-on-close>
      <el-form label-position="top" @submit.prevent>
        <el-form-item label="Phạm vi">
          <el-select v-model="form.scope" style="width:100%">
            <el-option label="Mặc định hệ thống" value="SystemDefault" />
            <el-option label="Loại dịch vụ" value="ServiceType" />
            <el-option label="Lớp / Nhóm" value="ClassGroup" />
            <el-option label="Học sinh" value="Student" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="form.scope === 'Student'" label="Học sinh" required>
          <el-select v-model="form.studentId" filterable style="width:100%">
            <el-option v-for="s in students" :key="s.id" :label="s.fullName" :value="s.id" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="form.scope === 'ClassGroup'" label="Lớp" required>
          <el-select v-model="form.classGroupId" filterable style="width:100%">
            <el-option v-for="c in classes" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="form.scope === 'ServiceType'" label="Loại dịch vụ" required>
          <el-select v-model="form.serviceType" style="width:100%">
            <el-option label="Tại trung tâm" value="AtCenter" />
            <el-option label="Tại trường" value="AtPartnerSchool" />
          </el-select>
        </el-form-item>
        <el-form-item label="Số tiền (VND)" required>
          <el-input-number v-model="form.amount" :min="0" :step="10000" style="width:100%" />
        </el-form-item>
        <el-form-item label="Hiệu lực từ" required>
          <el-date-picker v-model="form.effectiveFrom" type="date" style="width:100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <div class="dialog-actions">
          <el-button @click="visible = false" :disabled="saving">Hủy</el-button>
          <el-button type="primary" :loading="saving" @click="save">Lưu</el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import dayjs from 'dayjs'
import { ElMessage } from 'element-plus'
import api from '../api/client'
import PageHeader from '../components/PageHeader.vue'
import { useAsyncAction } from '../composables/useAsyncAction'
import type { ClassGroup, ServiceType, Student, TuitionRate, TuitionRateScope } from '../types'
import { rateScopeLabel } from '../utils/labels'

const rows = ref<TuitionRate[]>([])
const students = ref<Student[]>([])
const classes = ref<ClassGroup[]>([])
const loading = ref(false)
const visible = ref(false)
const form = reactive({
  scope: 'SystemDefault' as TuitionRateScope,
  studentId: undefined as string | undefined,
  classGroupId: undefined as string | undefined,
  serviceType: undefined as ServiceType | undefined,
  amount: 150000,
  effectiveFrom: new Date(),
})
const { loading: saving, run } = useAsyncAction()

function formatMoney(n: number) {
  return n.toLocaleString('vi-VN')
}

function resolveRef(row: TuitionRate) {
  if (row.scope === 'Student') {
    return students.value.find((s) => s.id === row.studentId)?.fullName || row.studentId || '—'
  }
  if (row.scope === 'ClassGroup') {
    return classes.value.find((c) => c.id === row.classGroupId)?.name || row.classGroupId || '—'
  }
  if (row.scope === 'ServiceType') {
    if (row.serviceType === 'AtCenter') return 'Tại trung tâm'
    if (row.serviceType === 'AtPartnerSchool') return 'Tại trường'
    return row.serviceType || '—'
  }
  return 'Hệ thống'
}

async function load() {
  loading.value = true
  try {
    rows.value = (await api.get<TuitionRate[]>('/tuition-rates')).data
  } catch (e: any) {
    ElMessage.error(e.message)
  } finally {
    loading.value = false
  }
}

function open() {
  form.scope = 'SystemDefault'
  form.studentId = undefined
  form.classGroupId = undefined
  form.serviceType = undefined
  form.amount = 150000
  form.effectiveFrom = new Date()
  visible.value = true
}

async function save() {
  if (form.scope === 'Student' && !form.studentId) {
    ElMessage.warning('Chọn học sinh')
    return
  }
  if (form.scope === 'ClassGroup' && !form.classGroupId) {
    ElMessage.warning('Chọn lớp')
    return
  }
  if (form.scope === 'ServiceType' && !form.serviceType) {
    ElMessage.warning('Chọn loại dịch vụ')
    return
  }
  if (!form.effectiveFrom) {
    ElMessage.warning('Chọn ngày hiệu lực')
    return
  }
  const ok = await run(async () => {
    await api.post('/tuition-rates', {
      scope: form.scope,
      studentId: form.studentId || null,
      classGroupId: form.classGroupId || null,
      serviceType: form.serviceType || null,
      amount: form.amount,
      effectiveFrom: dayjs(form.effectiveFrom).format('YYYY-MM-DD'),
      isActive: true,
    })
  }, 'Đã lưu đơn giá')
  if (ok) {
    visible.value = false
    await load()
  }
}

onMounted(async () => {
  try {
    ;[students.value, classes.value] = await Promise.all([
      api.get<Student[]>('/students').then((r) => r.data),
      api.get<ClassGroup[]>('/class-groups').then((r) => r.data),
    ])
  } catch (e: any) {
    ElMessage.error(e.message)
  }
  await load()
})
</script>
