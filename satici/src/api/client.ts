import axios from 'axios'

export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

// Token injection
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('supplier_access_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// 401 → refresh flow (satıcı oturumu: /api/supplier/auth/refresh)
api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config
    // Login denemesinin kendi 401'i yönlendirme/yenileme tetiklemesin — sayfa hatayı göstersin
    if (error.response?.status === 401 &&
        (original?.url?.includes('/supplier/auth/login') || window.location.pathname.endsWith('/login'))) {
      return Promise.reject(error)
    }
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true
      const refreshToken = localStorage.getItem('supplier_refresh_token')
      if (!refreshToken) {
        localStorage.clear()
        window.location.href = '/login'
        return Promise.reject(error)
      }
      try {
        const { data } = await axios.post('/api/supplier/auth/refresh', { refreshToken })
        const { accessToken, refreshToken: newRefresh } = data.data
        localStorage.setItem('supplier_access_token', accessToken)
        localStorage.setItem('supplier_refresh_token', newRefresh)
        original.headers.Authorization = `Bearer ${accessToken}`
        return api(original)
      } catch {
        localStorage.clear()
        window.location.href = '/login'
        return Promise.reject(error)
      }
    }
    return Promise.reject(error)
  },
)

export default api
