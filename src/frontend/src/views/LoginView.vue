<template>
  <div class="login-page">
    <div class="login-card surface-soft">
      <p class="brand">SECMS</p>
      <h1>Đăng nhập.</h1>
      <p class="lead">Quản lý trung tâm giáo dục đặc biệt — lịch, điểm danh, học phí.</p>

      <el-form @submit.prevent="onSubmit" label-position="top" class="form">
        <el-form-item label="Tài khoản">
          <el-input v-model="userName" placeholder="admin" size="large" clearable />
        </el-form-item>
        <el-form-item label="Mật khẩu">
          <el-input
            v-model="password"
            type="password"
            show-password
            placeholder="••••••••"
            size="large"
            @keyup.enter="onSubmit"
          />
        </el-form-item>
        <el-button
          type="primary"
          native-type="submit"
          :loading="loading"
          :disabled="!userName || !password"
          class="submit"
        >
          Tiếp tục
        </el-button>
      </el-form>

      <p class="hint">Demo · admin / Admin@123 · gv01 / Teacher@123</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useAsyncAction } from '../composables/useAsyncAction'

const auth = useAuthStore()
const router = useRouter()
const userName = ref('admin')
const password = ref('Admin@123')
const { loading, run } = useAsyncAction()

async function onSubmit() {
  await run(async () => {
    const u = await auth.login(userName.value.trim(), password.value)
    await router.push(u.role === 'Admin' ? '/schedule' : '/my/schedule')
  }, 'Đăng nhập thành công')
}
</script>

<style scoped>
.login-page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 24px;
  background: var(--canvas);
}

.login-card {
  width: min(420px, 100%);
}

.brand {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.08em;
  color: var(--text-muted);
}

h1 {
  margin: 12px 0 8px;
  font-size: 32px;
  font-weight: 650;
  line-height: 1.13;
}

.lead {
  margin: 0 0 28px;
  color: var(--text-muted);
  font-size: 16px;
  font-weight: 300;
  line-height: 1.38;
}

.form :deep(.el-form-item) {
  margin-bottom: 16px;
}

.submit {
  width: 100%;
  height: 44px !important;
  margin-top: 8px;
}

.hint {
  margin: 20px 0 0;
  color: var(--text-faint);
  font-size: 12px;
}
</style>
