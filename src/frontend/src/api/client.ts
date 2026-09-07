import axios from 'axios'
import { useAuthStore } from '../stores/auth'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
})

api.interceptors.request.use((config) => {
  const auth = useAuthStore()
  if (auth.token) {
    config.headers.Authorization = `Bearer ${auth.token}`
  }
  return config
})

api.interceptors.response.use(
  (r) => r,
  (err) => {
    const status = err.response?.status
    const data = err.response?.data
    if (status === 401) {
      try {
        useAuthStore().logout()
      } catch {
        localStorage.removeItem('secms_token')
        localStorage.removeItem('secms_user')
      }
      if (!window.location.pathname.includes('/login')) {
        window.location.assign('/login')
      }
    }
    const msg = data?.title || data?.message || err.message
    const error = new Error(typeof msg === 'string' ? msg : 'Lỗi hệ thống') as Error & {
      response?: unknown
      conflicts?: unknown
      status?: number
    }
    error.response = err.response
    error.conflicts = data?.conflicts
    error.status = status
    return Promise.reject(error)
  },
)

export default api
