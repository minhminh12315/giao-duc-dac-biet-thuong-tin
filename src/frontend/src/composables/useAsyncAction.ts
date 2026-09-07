import { ref } from 'vue'
import { ElMessage } from 'element-plus'

/** Wrap async button actions: loading + toast errors, prevent double submit */
export function useAsyncAction() {
  const loading = ref(false)

  async function run(fn: () => Promise<void>, successMsg?: string): Promise<boolean> {
    if (loading.value) return false
    loading.value = true
    try {
      await fn()
      if (successMsg) ElMessage.success(successMsg)
      return true
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Có lỗi xảy ra'
      ElMessage.error(msg)
      return false
    } finally {
      loading.value = false
    }
  }

  async function runResult<T>(fn: () => Promise<T>, successMsg?: string): Promise<T | null> {
    if (loading.value) return null
    loading.value = true
    try {
      const result = await fn()
      if (successMsg) ElMessage.success(successMsg)
      return result
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Có lỗi xảy ra'
      ElMessage.error(msg)
      return null
    } finally {
      loading.value = false
    }
  }

  return { loading, run, runResult }
}
