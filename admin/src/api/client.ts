import axios from 'axios'

export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

// Token injection
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('access_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// 401 → refresh flow
api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true
      const refreshToken = localStorage.getItem('refresh_token')
      if (!refreshToken) {
        localStorage.clear()
        window.location.href = '/admin/login'
        return Promise.reject(error)
      }
      try {
        const { data } = await axios.post('/api/auth/refresh', { refreshToken })
        const { accessToken, refreshToken: newRefresh } = data.data
        localStorage.setItem('access_token', accessToken)
        localStorage.setItem('refresh_token', newRefresh)
        original.headers.Authorization = `Bearer ${accessToken}`
        return api(original)
      } catch {
        localStorage.clear()
        window.location.href = '/admin/login'
        return Promise.reject(error)
      }
    }
    return Promise.reject(error)
  },
)

export default api
