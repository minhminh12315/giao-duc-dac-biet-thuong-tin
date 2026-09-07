<template>
  <div class="page">
    <PageHeader title="Giáo viên." subtitle="Hồ sơ và tài khoản đăng nhập.">
      <template #actions>
        <el-button type="primary" @click="open()">Thêm giáo viên</el-button>
      </template>
    </PageHeader>

    <div class="surface">
      <el-table :data="rows" v-loading="loading" empty-text="Chưa có giáo viên">
        <el-table-column prop="fullName" label="Họ tên" min-width="160" />
        <el-table-column prop="userName" label="Tài khoản" width="140" />
        <el-table-column prop="email" label="Email" min-width="180" />
        <el-table-column prop="phone" label="SĐT" width="130" />
        <el-table-column prop="specialization" label="Chuyên môn" min-width="140" />
        <el-table-column label="" width="90" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click.stop="open(row)">Sửa</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" :title="form.id ? 'Sửa giáo viên' : 'Thêm giáo viên'" width="520px" destroy-on-close>
      <el-form label-position="top" @submit.prevent>
        <template v-if="!form.id">
          <el-form-item label="Tài khoản" required><el-input v-model="form.userName" /></el-form-item>
          <el-form-item label="Email" required><el-input v-model="form.email" /></el-form-item>
          <el-form-item label="Mật khẩu" required><el-input v-model="form.password" type="password" show-password /></el-form-item>
        </template>
        <el-form-item label="Họ tên" required><el-input v-model="form.fullName" /></el-form-item>
        <el-form-item label="SĐT"><el-input v-model="form.phone" /></el-form-item>
        <el-form-item label="Chuyên môn"><el-input v-model="form.specialization" /></el-form-item>
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
import { useAsyncAction } from '../composables/useAsyncAction'
import type { Teacher } from '../types'

const rows = ref<Teacher[]>([])
const loading = ref(false)
const visible = ref(false)
const form = reactive({
  id: '',
  userName: '',
  email: '',
  password: 'Teacher@123',
  fullName: '',
  phone: '',
  specialization: '',
})
const { loading: saving, run } = useAsyncAction()

async function load() {
  loading.value = true
  try {
    rows.value = (await api.get<Teacher[]>('/teachers')).data
  } catch (e: any) {
    ElMessage.error(e.message || 'Không tải được danh sách')
  } finally {
    loading.value = false
  }
}

function open(row?: Teacher) {
  form.id = row?.id || ''
  form.userName = row?.userName || ''
  form.email = row?.email || ''
  form.password = 'Teacher@123'
  form.fullName = row?.fullName || ''
  form.phone = row?.phone || ''
  form.specialization = row?.specialization || ''
  visible.value = true
}

async function save() {
  if (!form.fullName.trim()) {
    ElMessage.warning('Vui lòng nhập họ tên')
    return
  }
  if (!form.id && (!form.userName.trim() || !form.email.trim() || !form.password)) {
    ElMessage.warning('Vui lòng điền đủ tài khoản, email và mật khẩu')
    return
  }
  const ok = await run(async () => {
    if (form.id) {
      await api.put(`/teachers/${form.id}`, {
        fullName: form.fullName.trim(),
        phone: form.phone || null,
        specialization: form.specialization || null,
        isActive: true,
      })
    } else {
      await api.post('/teachers', {
        userName: form.userName.trim(),
        email: form.email.trim(),
        password: form.password,
        fullName: form.fullName.trim(),
        phone: form.phone || null,
        specialization: form.specialization || null,
      })
    }
  }, 'Đã lưu giáo viên')
  if (ok) {
    visible.value = false
    await load()
  }
}

onMounted(load)
</script>
