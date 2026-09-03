import axios from 'axios'

interface ApiErrorBody {
  error?: unknown
}

export function apiErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError<ApiErrorBody>(error)) {
    const message = error.response?.data?.error
    if (typeof message === 'string' && message.trim()) return message
  }

  return fallback
}

export function apiErrorStatus(error: unknown): number | undefined {
  return axios.isAxiosError(error) ? error.response?.status : undefined
}
