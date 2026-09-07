import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '../api/client'
import type { LoginResponse } from '../types'

const TOKEN_KEY = 'secms_token'
const USER_KEY = 'secms_user'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem(TOKEN_KEY))
  const user = ref<LoginResponse | null>(JSON.parse(localStorage.getItem(USER_KEY) || 'null'))

  const isAuthenticated = computed(() => !!token.value)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isTeacher = computed(() => user.value?.role === 'Teacher')

  async function login(userName: string, password: string) {
    const { data } = await api.post<LoginResponse>('/auth/login', { userName, password })
    token.value = data.accessToken
    user.value = data
    localStorage.setItem(TOKEN_KEY, data.accessToken)
    localStorage.setItem(USER_KEY, JSON.stringify(data))
    return data
  }

  function logout() {
    token.value = null
    user.value = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  }

  return { token, user, isAuthenticated, isAdmin, isTeacher, login, logout }
})
