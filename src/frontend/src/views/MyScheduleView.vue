<template>
  <ScheduleView mode="teacher" />
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import ScheduleView from './ScheduleView.vue'

const auth = useAuthStore()
const router = useRouter()

onMounted(() => {
  // Admin should use system schedule; this page is for the assigned teacher only.
  if (auth.isAdmin && !auth.user?.teacherProfileId) {
    router.replace('/schedule')
  }
})
</script>
