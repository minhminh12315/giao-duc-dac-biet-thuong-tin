<template>
  <div class="shell">
    <aside class="aside">
      <div class="brand-block">
        <div class="mark">S</div>
        <div>
          <strong>SECMS</strong>
          <span>Giáo dục đặc biệt</span>
        </div>
      </div>

      <nav class="nav" aria-label="Menu chính">
        <RouterLink
          v-for="item in menu"
          :key="item.to"
          :to="item.to"
          class="nav-item"
          :class="{ active: isActive(item.to) }"
          @click="onNavClick"
        >
          {{ item.label }}
        </RouterLink>
      </nav>

      <div class="aside-foot">
        <div class="who">
          <b>{{ auth.user?.displayName }}</b>
          <span>{{ auth.user?.role === 'Admin' ? 'Quản trị viên' : 'Giáo viên' }}</span>
        </div>
        <el-button class="logout" @click="logout" :loading="loggingOut">Đăng xuất</el-button>
      </div>
    </aside>

    <main class="main">
      <router-view />
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const loggingOut = ref(false)

const menu = computed(() => {
  const items: { to: string; label: string }[] = []
  if (auth.isAdmin) {
    items.push(
      { to: '/schedule', label: 'Lịch hệ thống' },
      { to: '/facilities', label: 'Cơ sở' },
      { to: '/teachers', label: 'Giáo viên' },
      { to: '/students', label: 'Học sinh' },
      { to: '/class-groups', label: 'Lớp / Nhóm' },
      { to: '/billing/rates', label: 'Đơn giá' },
      { to: '/billing/invoices', label: 'Học phí' },
    )
  } else {
    items.push(
      { to: '/my/schedule', label: 'Lịch của tôi' },
      { to: '/students', label: 'Học sinh' },
    )
  }
  return items
})

function isActive(to: string) {
  return route.path === to || route.path.startsWith(to + '/')
}

/** Clear leftover Element Plus / FullCalendar overlays that can trap clicks over the sidebar */
function onNavClick() {
  document.body.classList.remove('el-popup-parent--hidden')
  document.querySelectorAll('.el-overlay').forEach((el) => {
    const node = el as HTMLElement
    if (node.style.display === 'none') return
    // Only remove orphan overlays without an open dialog/drawer child still mounted
    if (!node.querySelector('.el-drawer, .el-dialog, .el-message-box')) {
      node.remove()
    }
  })
}

async function logout() {
  loggingOut.value = true
  try {
    auth.logout()
    await router.push('/login')
  } finally {
    loggingOut.value = false
  }
}
</script>

<style scoped>
.shell {
  min-height: 100vh;
  display: grid;
  grid-template-columns: 260px 1fr;
  background: var(--canvas);
}

.aside {
  background: var(--canvas-soft);
  border-right: 1px solid var(--hairline-soft);
  padding: 24px 16px;
  display: flex;
  flex-direction: column;
  gap: 24px;
  position: sticky;
  top: 0;
  height: 100vh;
  z-index: 200;
  isolation: isolate;
  pointer-events: auto;
}

.brand-block {
  display: flex;
  gap: 12px;
  align-items: center;
  padding: 0 8px;
}

.mark {
  width: 40px;
  height: 40px;
  border-radius: 30%;
  background: var(--ink);
  color: #fff;
  display: grid;
  place-items: center;
  font-weight: 650;
}

.brand-block strong {
  display: block;
  font-size: 16px;
  font-weight: 650;
}

.brand-block span {
  display: block;
  font-size: 12px;
  color: var(--text-muted);
}

.nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
}

.nav-item {
  display: block;
  width: 100%;
  padding: 10px 14px;
  border: none;
  border-radius: 16px;
  background: transparent;
  color: var(--text-muted);
  font: inherit;
  font-weight: 600;
  font-size: 14px;
  text-align: left;
  text-decoration: none;
  cursor: pointer;
  position: relative;
  z-index: 1;
  transition: background 0.15s ease, color 0.15s ease;
}

.nav-item:hover {
  background: var(--canvas);
  color: var(--ink);
}

.nav-item.active {
  background: var(--canvas);
  color: var(--ink);
  box-shadow: inset 3px 0 0 var(--ink);
}

.aside-foot {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 8px;
}

.who b {
  display: block;
  font-size: 14px;
  font-weight: 600;
}

.who span {
  font-size: 12px;
  color: var(--text-muted);
}

.logout {
  width: 100%;
}

.main {
  position: relative;
  z-index: 0;
  padding: 32px;
  min-width: 0;
  overflow-x: clip;
}

@media (max-width: 960px) {
  .shell {
    grid-template-columns: 1fr;
  }
  .aside {
    position: static;
    height: auto;
    border-right: none;
    border-bottom: 1px solid var(--hairline);
    z-index: 200;
  }
  .nav {
    flex-direction: row;
    flex-wrap: wrap;
  }
  .main {
    padding: 20px 16px;
  }
}
</style>
