<template>
  <div class="page">
    <PageHeader title="Học phí." subtitle="Tính động: số ca Có đi học × đơn giá tương ứng.">
      <template #actions>
        <el-button type="primary" @click="genVisible = true">Tạo hóa đơn tháng</el-button>
      </template>
    </PageHeader>

    <div class="surface">
      <el-table
        :data="rows"
        v-loading="loading"
        empty-text="Chưa có hóa đơn"
        highlight-current-row
        @row-click="onRowClick"
      >
        <el-table-column prop="studentName" label="Học sinh" min-width="160" />
        <el-table-column label="Kỳ" width="120">
          <template #default="{ row }">{{ row.periodMonth }}/{{ row.periodYear }}</template>
        </el-table-column>
        <el-table-column label="Tổng tiền" width="160">
          <template #default="{ row }">{{ row.totalAmount.toLocaleString('vi-VN') }} {{ row.currency }}</template>
        </el-table-column>
        <el-table-column label="Trạng thái" width="130">
          <template #default="{ row }">{{ invoiceStatusLabel(row.status) }}</template>
        </el-table-column>
        <el-table-column prop="version" label="Ver" width="70" />
        <el-table-column label="" width="100" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click.stop="detail = row">Chi tiết</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-drawer v-model="detailOpen" title="Chi tiết hóa đơn" size="480px">
      <template v-if="detail">
        <p class="detail-head">
          <b>{{ detail.studentName }}</b>
          <span>{{ detail.periodMonth }}/{{ detail.periodYear }}</span>
        </p>
        <p class="detail-total">
          Tổng: <b>{{ detail.totalAmount.toLocaleString('vi-VN') }} VND</b>
        </p>
        <el-table :data="detail.lines" size="small" empty-text="Không có dòng tính phí">
          <el-table-column prop="description" label="Ca" min-width="180" />
          <el-table-column label="Đơn giá" width="120">
            <template #default="{ row }">{{ row.unitRate.toLocaleString('vi-VN') }}</template>
          </el-table-column>
        </el-table>
      </template>
    </el-drawer>

    <el-dialog v-model="genVisible" title="Tạo hóa đơn" width="420px" destroy-on-close>
      <el-form label-position="top" @submit.prevent>
        <el-form-item label="Học sinh" required>
          <el-select v-model="gen.studentId" filterable style="width:100%">
            <el-option v-for="s in students" :key="s.id" :label="s.fullName" :value="s.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Tháng">
          <el-input-number v-model="gen.month" :min="1" :max="12" />
        </el-form-item>
        <el-form-item label="Năm">
          <el-input-number v-model="gen.year" :min="2020" :max="2100" />
        </el-form-item>
        <el-checkbox v-model="gen.regenerate">Tạo lại nếu đã có</el-checkbox>
      </el-form>
      <template #footer>
        <div class="dialog-actions">
          <el-button @click="genVisible = false" :disabled="generating">Hủy</el-button>
          <el-button type="primary" :loading="generating" :disabled="!gen.studentId" @click="generate">Tạo</el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import api from '../api/client'
import PageHeader from '../components/PageHeader.vue'
import { useAsyncAction } from '../composables/useAsyncAction'
import type { Invoice, Student } from '../types'
import { invoiceStatusLabel } from '../utils/labels'

const rows = ref<Invoice[]>([])
const students = ref<Student[]>([])
const loading = ref(false)
const genVisible = ref(false)
const detail = ref<Invoice | null>(null)
const detailOpen = computed({
  get: () => !!detail.value,
  set: (v) => {
    if (!v) detail.value = null
  },
})
const now = new Date()
const gen = reactive({
  studentId: '',
  year: now.getFullYear(),
  month: now.getMonth() + 1,
  regenerate: false,
})
const { loading: generating, runResult } = useAsyncAction()

function onRowClick(row: Invoice) {
  detail.value = row
}

async function load() {
  loading.value = true
  try {
    rows.value = (await api.get<Invoice[]>('/invoices')).data
  } catch (e: any) {
    ElMessage.error(e.message)
  } finally {
    loading.value = false
  }
}

async function generate() {
  if (!gen.studentId) {
    ElMessage.warning('Chọn học sinh')
    return
  }
  const data = await runResult(async () => {
    const res = await api.post<Invoice>('/invoices/generate', { ...gen })
    return res.data
  }, 'Đã tạo hóa đơn')
  if (data) {
    genVisible.value = false
    detail.value = data
    await load()
  }
}

onMounted(async () => {
  try {
    students.value = (await api.get<Student[]>('/students')).data
    gen.studentId = students.value[0]?.id || ''
  } catch (e: any) {
    ElMessage.error(e.message)
  }
  await load()
})
</script>

<style scoped>
.detail-head {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  color: var(--text-muted);
}
.detail-head b { color: var(--ink); }
.detail-total {
  font-size: 18px;
  margin: 8px 0 16px;
}
</style>
