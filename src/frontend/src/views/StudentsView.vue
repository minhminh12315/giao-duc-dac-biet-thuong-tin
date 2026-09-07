<template>
  <div class="page">
    <PageHeader title="Học sinh." subtitle="Hồ sơ · gắn cơ sở · theo dõi phát triển.">
      <template #actions>
        <el-input
          v-model="search"
          placeholder="Tìm tên..."
          clearable
          style="width: 220px"
          @clear="load"
          @keyup.enter="load"
        />
        <el-button @click="load">Tìm</el-button>
        <el-button v-if="auth.isAdmin" type="primary" @click="open()">Thêm học sinh</el-button>
      </template>
    </PageHeader>

    <div class="surface">
      <el-table :data="rows" v-loading="loading" empty-text="Chưa có học sinh">
        <el-table-column prop="fullName" label="Họ tên" min-width="160" />
        <el-table-column label="Cơ sở" min-width="200">
          <template #default="{ row }">
            <span>{{ row.facilityName }}</span>
            <el-tag
              size="small"
              :type="row.facilityType === 'PartnerSchool' ? 'warning' : 'success'"
              effect="dark"
              round
              style="margin-left: 8px"
            >
              {{ row.facilityType === 'MainCenter' ? 'Chính' : 'Đối tác' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="guardianName" label="Phụ huynh" min-width="140" />
        <el-table-column prop="guardianPhone" label="SĐT" width="130" />
        <el-table-column label="Trạng thái" width="130">
          <template #default="{ row }">{{ studentStatusLabel(row.status) }}</template>
        </el-table-column>
        <el-table-column label="" width="200" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click.stop="$router.push(`/students/${row.id}/progress`)">Tiến trình</el-button>
            <el-button v-if="auth.isAdmin" link type="primary" @click.stop="open(row)">Sửa</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" :title="form.id ? 'Sửa học sinh' : 'Thêm học sinh'" width="520px" destroy-on-close>
      <el-form label-position="top" @submit.prevent>
        <el-form-item label="Họ tên" required><el-input v-model="form.fullName" /></el-form-item>
        <el-form-item label="Cơ sở" required>
          <el-select v-model="form.facilityId" style="width:100%" filterable>
            <el-option v-for="f in facilities" :key="f.id" :label="f.name" :value="f.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Phụ huynh"><el-input v-model="form.guardianName" /></el-form-item>
        <el-form-item label="SĐT PH"><el-input v-model="form.guardianPhone" /></el-form-item>
        <el-form-item label="Ghi chú y tế"><el-input v-model="form.medicalNotes" type="textarea" :rows="3" /></el-form-item>
        <el-form-item label="Trạng thái">
          <el-select v-model="form.status" style="width:100%">
            <el-option label="Đang học" value="Active" />
            <el-option label="Ngừng" value="Inactive" />
            <el-option label="Đã tốt nghiệp" value="Graduated" />
          </el-select>
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
import { ElMessage } from 'element-plus'
import api from '../api/client'
import PageHeader from '../components/PageHeader.vue'
import { studentStatusLabel } from '../utils/labels'
import { useAsyncAction } from '../composables/useAsyncAction'
import { useAuthStore } from '../stores/auth'
import type { Facility, Student } from '../types'

const auth = useAuthStore()
const rows = ref<Student[]>([])
const facilities = ref<Facility[]>([])
const loading = ref(false)
const visible = ref(false)
const search = ref('')
const form = reactive({
  id: '',
  fullName: '',
  facilityId: '',
  guardianName: '',
  guardianPhone: '',
  medicalNotes: '',
  status: 'Active' as Student['status'],
})
const { loading: saving, run } = useAsyncAction()

async function load() {
  loading.value = true
  try {
    rows.value = (await api.get<Student[]>('/students', { params: { search: search.value || undefined } })).data
  } catch (e: any) {
    ElMessage.error(e.message || 'Không tải được danh sách')
  } finally {
    loading.value = false
  }
}

function open(row?: Student) {
  form.id = row?.id || ''
  form.fullName = row?.fullName || ''
  form.facilityId = row?.facilityId || facilities.value[0]?.id || ''
  form.guardianName = row?.guardianName || ''
  form.guardianPhone = row?.guardianPhone || ''
  form.medicalNotes = row?.medicalNotes || ''
  form.status = row?.status || 'Active'
  visible.value = true
}

async function save() {
  if (!form.fullName.trim() || !form.facilityId) {
    ElMessage.warning('Vui lòng nhập họ tên và chọn cơ sở')
    return
  }
  const ok = await run(async () => {
    const payload = {
      fullName: form.fullName.trim(),
      facilityId: form.facilityId,
      guardianName: form.guardianName || null,
      guardianPhone: form.guardianPhone || null,
      medicalNotes: form.medicalNotes || null,
      status: form.status,
    }
    if (form.id) await api.put(`/students/${form.id}`, payload)
    else await api.post('/students', payload)
  }, 'Đã lưu học sinh')
  if (ok) {
    visible.value = false
    await load()
  }
}

onMounted(async () => {
  try {
    facilities.value = (await api.get<Facility[]>('/facilities')).data
  } catch (e: any) {
    ElMessage.error(e.message)
  }
  await load()
})
</script>
