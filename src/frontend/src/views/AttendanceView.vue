<template>
  <div class="page">
    <PageHeader
      title="Điểm danh."
      subtitle="Bắt buộc nhận xét từng học sinh khi hoàn tất."
    >
      <template #actions>
        <el-button @click="goBack" :disabled="saving">Quay lại</el-button>
        <el-button :loading="savingDraft" :disabled="saving" @click="saveDraft">Lưu nháp</el-button>
        <el-button type="primary" :loading="savingFinalize" :disabled="saving" @click="finalize">
          Hoàn tất điểm danh
        </el-button>
      </template>
    </PageHeader>

    <div class="surface">
      <el-table :data="items" v-loading="loading" empty-text="Không có học sinh trong ca">
        <el-table-column prop="studentName" label="Học sinh" min-width="160" />
        <el-table-column label="Trạng thái" width="170">
          <template #default="{ row }">
            <el-select v-model="row.status" style="width:100%">
              <el-option label="Chưa điểm danh" value="NotMarked" />
              <el-option label="Có đi học" value="Present" />
              <el-option label="Nghỉ" value="Absent" />
              <el-option label="Nghỉ có phép" value="Excused" />
            </el-select>
          </template>
        </el-table-column>
        <el-table-column label="Nhận xét" min-width="280">
          <template #default="{ row }">
            <el-input
              v-model="row.comment"
              type="textarea"
              :rows="2"
              placeholder="Tình trạng / tiến bộ hôm nay..."
            />
          </template>
        </el-table-column>
        <el-table-column label="Mức (1–5)" width="130">
          <template #default="{ row }">
            <el-input-number
              v-model="row.moodOrLevel"
              :min="1"
              :max="5"
              controls-position="right"
              style="width:100%"
            />
          </template>
        </el-table-column>
      </el-table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import api from '../api/client'
import PageHeader from '../components/PageHeader.vue'
import { useAsyncAction } from '../composables/useAsyncAction'
import type { AttendanceItem } from '../types'

const route = useRoute()
const router = useRouter()
const sessionId = computed(() => route.params.id as string)
const items = ref<AttendanceItem[]>([])
const loading = ref(false)
const { loading: savingDraft, run: runDraft } = useAsyncAction()
const { loading: savingFinalize, run: runFinalize } = useAsyncAction()
const saving = computed(() => savingDraft.value || savingFinalize.value)

async function load() {
  loading.value = true
  try {
    const { data } = await api.get<AttendanceItem[]>(`/sessions/${sessionId.value}/attendance`)
    items.value = data.map((x) => ({
      ...x,
      status: x.status,
      comment: x.comment || '',
      moodOrLevel: x.moodOrLevel ?? undefined,
    }))
  } catch (e: any) {
    ElMessage.error(e.message || 'Không tải được điểm danh')
  } finally {
    loading.value = false
  }
}

function payload() {
  return {
    items: items.value.map((i) => ({
      studentId: i.studentId,
      status: i.status,
      comment: i.comment,
      moodOrLevel: i.moodOrLevel,
      tags: i.tags || [],
    })),
  }
}

function goBack() {
  router.back()
}

async function saveDraft() {
  await runDraft(async () => {
    await api.put(`/sessions/${sessionId.value}/attendance/draft`, payload())
  }, 'Đã lưu nháp')
}

async function finalize() {
  if (items.value.some((i) => i.status === 'NotMarked')) {
    ElMessage.warning('Vui lòng chọn trạng thái điểm danh cho tất cả học sinh')
    return
  }
  const missing = items.value.filter((i) => !String(i.comment || '').trim())
  if (missing.length) {
    ElMessage.warning('Vui lòng nhập nhận xét cho tất cả học sinh')
    return
  }
  try {
    await ElMessageBox.confirm(
      'Hoàn tất điểm danh? Dữ liệu sẽ dùng để tính học phí và theo dõi phát triển.',
      'Xác nhận',
      { confirmButtonText: 'Hoàn tất', cancelButtonText: 'Hủy', type: 'warning' },
    )
  } catch {
    return
  }
  const ok = await runFinalize(async () => {
    await api.post(`/sessions/${sessionId.value}/attendance/finalize`, payload())
  }, 'Đã hoàn tất điểm danh')
  if (ok) await load()
}

watch(sessionId, () => load(), { immediate: true })
</script>
