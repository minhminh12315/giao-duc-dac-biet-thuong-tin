<template>
  <div class="page">
    <PageHeader title="Cơ sở." subtitle="Cơ sở chính và trường đối tác trong mô hình hybrid.">
      <template #actions>
        <el-button type="primary" @click="open()">Thêm cơ sở</el-button>
      </template>
    </PageHeader>

    <div class="surface">
      <el-table :data="rows" v-loading="loading" empty-text="Chưa có cơ sở">
        <el-table-column prop="name" label="Tên" min-width="180" />
        <el-table-column label="Loại" width="160">
          <template #default="{ row }">
            <el-tag :type="row.type === 'PartnerSchool' ? 'warning' : 'success'" effect="dark" round>
              {{ row.type === 'MainCenter' ? 'Cơ sở chính' : 'Trường đối tác' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="address" label="Địa chỉ" min-width="200" />
        <el-table-column prop="contactPhone" label="Điện thoại" width="140" />
        <el-table-column label="" width="100" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click.stop="open(row)">Sửa</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" :title="form.id ? 'Sửa cơ sở' : 'Thêm cơ sở'" width="480px" destroy-on-close>
      <el-form label-position="top" @submit.prevent>
        <el-form-item label="Tên" required>
          <el-input v-model="form.name" placeholder="Tên cơ sở" />
        </el-form-item>
        <el-form-item label="Loại" required>
          <el-select v-model="form.type" style="width:100%">
            <el-option label="Cơ sở chính" value="MainCenter" />
            <el-option label="Trường đối tác" value="PartnerSchool" />
          </el-select>
        </el-form-item>
        <el-form-item label="Địa chỉ"><el-input v-model="form.address" /></el-form-item>
        <el-form-item label="Điện thoại"><el-input v-model="form.contactPhone" /></el-form-item>
      </el-form>
      <template #footer>
        <div class="dialog-actions">
          <el-button @click="visible = false" :disabled="saving">Hủy</el-button>
          <el-button type="primary" :loading="saving" :disabled="!form.name.trim()" @click="save">Lưu</el-button>
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
import type { Facility, FacilityType } from '../types'

const rows = ref<Facility[]>([])
const loading = ref(false)
const visible = ref(false)
const form = reactive({ id: '', name: '', type: 'MainCenter' as FacilityType, address: '', contactPhone: '' })
const { loading: saving, run } = useAsyncAction()

async function load() {
  loading.value = true
  try {
    rows.value = (await api.get<Facility[]>('/facilities')).data
  } catch (e: any) {
    ElMessage.error(e.message || 'Không tải được danh sách')
  } finally {
    loading.value = false
  }
}

function open(row?: Facility) {
  form.id = row?.id || ''
  form.name = row?.name || ''
  form.type = row?.type || 'MainCenter'
  form.address = row?.address || ''
  form.contactPhone = row?.contactPhone || ''
  visible.value = true
}

async function save() {
  if (!form.name.trim()) {
    ElMessage.warning('Vui lòng nhập tên cơ sở')
    return
  }
  const ok = await run(async () => {
    const payload = {
      name: form.name.trim(),
      type: form.type,
      address: form.address || null,
      contactPhone: form.contactPhone || null,
      isActive: true,
    }
    if (form.id) await api.put(`/facilities/${form.id}`, payload)
    else await api.post('/facilities', payload)
  }, 'Đã lưu cơ sở')
  if (ok) {
    visible.value = false
    await load()
  }
}

onMounted(load)
</script>
