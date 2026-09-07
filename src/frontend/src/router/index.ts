import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: () => import('../views/LoginView.vue'), meta: { public: true } },
    {
      path: '/',
      component: () => import('../views/AppLayout.vue'),
      children: [
        { path: '', redirect: '/schedule' },
        { path: 'schedule', name: 'schedule', component: () => import('../views/ScheduleView.vue'), meta: { roles: ['Admin'] } },
        { path: 'my/schedule', name: 'my-schedule', component: () => import('../views/MyScheduleView.vue'), meta: { roles: ['Teacher', 'Admin'] } },
        { path: 'my/sessions/:id/attendance', name: 'attendance', component: () => import('../views/AttendanceView.vue'), meta: { roles: ['Teacher', 'Admin'] } },
        { path: 'facilities', name: 'facilities', component: () => import('../views/FacilitiesView.vue'), meta: { roles: ['Admin'] } },
        { path: 'teachers', name: 'teachers', component: () => import('../views/TeachersView.vue'), meta: { roles: ['Admin'] } },
        { path: 'students', name: 'students', component: () => import('../views/StudentsView.vue'), meta: { roles: ['Admin', 'Teacher'] } },
        { path: 'students/:id/progress', name: 'progress', component: () => import('../views/ProgressView.vue'), meta: { roles: ['Admin', 'Teacher'] } },
        { path: 'class-groups', name: 'class-groups', component: () => import('../views/ClassGroupsView.vue'), meta: { roles: ['Admin'] } },
        { path: 'billing/rates', name: 'rates', component: () => import('../views/RatesView.vue'), meta: { roles: ['Admin'] } },
        { path: 'billing/invoices', name: 'invoices', component: () => import('../views/InvoicesView.vue'), meta: { roles: ['Admin'] } },
      ],
    },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.meta.public) {
    if (auth.isAuthenticated) return auth.isAdmin ? '/schedule' : '/my/schedule'
    return true
  }
  if (!auth.isAuthenticated) return '/login'
  const roles = to.meta.roles as string[] | undefined
  if (roles && auth.user && !roles.includes(auth.user.role)) {
    return auth.isAdmin ? '/schedule' : '/my/schedule'
  }
  return true
})

export default router
