<template>
  <div class="page">
    <PageHeader title="Lớp / Nhóm." subtitle="Gán học sinh và loại dịch vụ (trung tâm / trường).">
      <template #actions>
        <el-button type="primary" @click="open()">Thêm lớp</el-button>
      </template>
    </PageHeader>

    <div class="surface">
      <el-table :data="rows" v-loading="loading" empty-text="Chưa có lớp">
        <el-table-column prop="name" label="Tên lớp" min-width="140" />
        <el-table-column prop="facilityName" label="Cơ sở" min-width="160" />
        <el-table-column label="Dịch vụ" width="160">
          <template #default="{ row }">
            {{ row.serviceType === 'AtCenter' ? 'Tại trung tâm' : 'Tại trường' }}
          </template>
        </el-table-column>
        <el-table-column label="HS" width="80">
          <template #default="{ row }">{{ row.studentIds.length }}</template>
        </el-table-column>
        <el-table-column label="" width="180" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click.stop="open(row)">Sửa</el-button>
            <el-button link type="primary" @click.stop="openStudents(row)">Học sinh</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" :title="form.id ? 'Sửa lớp' : 'Thêm lớp'" width="480px" destroy-on-close>
      <el-form label-position="top" @submit.prevent>
        <el-form-item label="Tên" required><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="Cơ sở">
          <el-select v-model="form.facilityId" clearable style="width:100%">
            <el-option v-for="f in facilities" :key="f.id" :label="f.name" :value="f.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="Loại dịch vụ">
          <el-select v-model="form.serviceType" style="width:100%">
            <el-option label="Tại trung tâm" value="AtCenter" />
            <el-option label="Tại trường đối tác" value="AtPartnerSchool" />
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

    <el-dialog v-model="studentsVisible" title="Gán học sinh vào lớp" width="480px" destroy-on-close>
      <el-select v-model="selectedStudents" multiple filterable style="width:100%" placeholder="Chọn học sinh">
        <el-option v-for="s in students" :key="s.id" :label="s.fullName" :value="s.id" />
      </el-select>
      <template #footer>
        <div class="dialog-actions">
          <el-button @click="studentsVisible = false" :disabled="savingStudents">Hủy</el-button>
          <el-button type="primary" :loading="savingStudents" @click="saveStudents">Lưu</el-button>
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
import { useAsyncAction } from '../composables/useAsyncAction'
import type { ClassGroup, Facility, ServiceType, Student } from '../types'

const rows = ref<ClassGroup[]>([])
const facilities = ref<Facility[]>([])
const students = ref<Student[]>([])
const loading = ref(false)
const visible = ref(false)
const studentsVisible = ref(false)
const selectedStudents = ref<string[]>([])
const editingId = ref('')
const form = reactive({
  id: '',
  name: '',
  facilityId: undefined as string | undefined,
  serviceType: 'AtCenter' as ServiceType,
})
const { loading: saving, run } = useAsyncAction()
const { loading: savingStudents, run: runStudents } = useAsyncAction()

async function load() {
  loading.value = true
  try {
    rows.value = (await api.get<ClassGroup[]>('/class-groups')).data
  } catch (e: any) {
    ElMessage.error(e.message)
  } finally {
    loading.value = false
  }
}

function open(row?: ClassGroup) {
  form.id = row?.id || ''
  form.name = row?.name || ''
  form.facilityId = row?.facilityId || undefined
  form.serviceType = row?.serviceType || 'AtCenter'
  visible.value = true
}

async function save() {
  if (!form.name.trim()) {
    ElMessage.warning('Vui lòng nhập tên lớp')
    return
  }
  const ok = await run(async () => {
    const payload = {
      name: form.name.trim(),
      facilityId: form.facilityId || null,
      serviceType: form.serviceType,
      isActive: true,
    }
    if (form.id) await api.put(`/class-groups/${form.id}`, payload)
    else await api.post('/class-groups', payload)
  }, 'Đã lưu lớp')
  if (ok) {
    visible.value = false
    await load()
  }
}

function openStudents(row: ClassGroup) {
  editingId.value = row.id
  selectedStudents.value = [...row.studentIds]
  studentsVisible.value = true
}

async function saveStudents() {
  const ok = await runStudents(async () => {
    await api.put(`/class-groups/${editingId.value}/students`, { studentIds: selectedStudents.value })
  }, 'Đã cập nhật học sinh')
  if (ok) {
    studentsVisible.value = false
    await load()
  }
}

onMounted(async () => {
  try {
    ;[facilities.value, students.value] = await Promise.all([
      api.get<Facility[]>('/facilities').then((r) => r.data),
      api.get<Student[]>('/students').then((r) => r.data),
    ])
  } catch (e: any) {
    ElMessage.error(e.message)
  }
  await load()
})
</script>
