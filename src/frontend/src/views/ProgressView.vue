<template>
  <div class="page">
    <PageHeader
      :title="progress ? `${progress.studentName}.` : 'Tiến trình.'"
      subtitle="Tổng hợp từ điểm danh và nhận xét giáo viên."
    >
      <template #actions>
        <el-button @click="$router.push('/students')">Quay lại danh sách</el-button>
      </template>
    </PageHeader>

    <div class="kpi-grid" v-if="progress">
      <div class="kpi"><b>{{ progress.presentCount }}</b><span>Có mặt</span></div>
      <div class="kpi"><b>{{ progress.absentCount }}</b><span>Nghỉ</span></div>
      <div class="kpi"><b>{{ progress.attendanceRate }}%</b><span>Chuyên cần</span></div>
      <div class="kpi"><b>{{ progress.avgMood ?? '—' }}</b><span>TB mức độ</span></div>
    </div>

    <div class="surface" v-loading="loading">
      <div ref="chartRef" style="height: 280px"></div>
    </div>

    <div class="surface">
      <h3 class="section-title">Nhật ký nhận xét</h3>
      <el-timeline v-if="progress && progress.comments.length">
        <el-timeline-item v-for="(c, i) in progress.comments" :key="i" :timestamp="formatTime(c.at)">
          <strong>{{ c.teacherName }}</strong>
          <el-tag size="small" round style="margin-left: 8px">{{ attendanceStatusLabel(c.status) }}</el-tag>
          <p class="comment">{{ c.content }}</p>
        </el-timeline-item>
      </el-timeline>
      <el-empty v-else-if="!loading" description="Chưa có nhận xét" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { nextTick, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import dayjs from 'dayjs'
import * as echarts from 'echarts'
import { ElMessage } from 'element-plus'
import api from '../api/client'
import PageHeader from '../components/PageHeader.vue'
import type { Progress } from '../types'
import { attendanceStatusLabel } from '../utils/labels'

const route = useRoute()
const progress = ref<Progress | null>(null)
const chartRef = ref<HTMLDivElement>()
const loading = ref(false)
let chart: echarts.ECharts | null = null

function formatTime(v: string) {
  return dayjs(v).format('DD/MM/YYYY HH:mm')
}

async function load() {
  loading.value = true
  progress.value = null
  try {
    const { data } = await api.get<Progress>(`/students/${route.params.id}/progress`)
    progress.value = data
    await nextTick()
    if (!chartRef.value) return
    if (!chart) {
      chart = echarts.init(chartRef.value)
      window.addEventListener('resize', resize)
    }
    chart.setOption({
      tooltip: { trigger: 'axis' },
      xAxis: {
        type: 'category',
        data: data.moodSeries.map((p) => dayjs(p.at).format('DD/MM')),
        axisLine: { lineStyle: { color: '#e0e0e0' } },
        axisLabel: { color: '#707070' },
      },
      yAxis: {
        type: 'value',
        min: 1,
        max: 5,
        name: 'Mức',
        splitLine: { lineStyle: { color: '#f0f0f0' } },
        axisLabel: { color: '#707070' },
      },
      series: [
        {
          type: 'line',
          smooth: true,
          data: data.moodSeries.map((p) => p.mood),
          areaStyle: { color: 'rgba(0,102,255,0.08)' },
          itemStyle: { color: '#0066ff' },
          lineStyle: { color: '#141414', width: 2 },
        },
      ],
      grid: { left: 40, right: 20, top: 30, bottom: 30 },
    })
  } catch (e: any) {
    ElMessage.error(e.message || 'Không tải được tiến trình')
  } finally {
    loading.value = false
  }
}

function resize() {
  chart?.resize()
}

watch(() => route.params.id, () => load(), { immediate: true })

onUnmounted(() => {
  window.removeEventListener('resize', resize)
  chart?.dispose()
  chart = null
})
</script>

<style scoped>
.section-title {
  margin: 0 0 16px;
  font-size: 20px;
  font-weight: 650;
}
.comment {
  margin: 8px 0 0;
  color: var(--ink-soft);
  font-weight: 450;
  line-height: 1.43;
}
</style>
