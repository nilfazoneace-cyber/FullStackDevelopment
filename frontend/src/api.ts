// THE ONLY file that talks to the network. Components never call fetch() themselves.
// Responsibilities:
//   1. add "Authorization: Bearer <token>" to every request
//   2. turn error responses into one predictable ApiError
//   3. tell the app about 401 (session over) and 403 (not allowed) through callbacks

import type {
  Bug,
  BugListParams,
  CreateBugInput,
  FieldErrors,
  LoginResponse,
  PagedResult,
  UpdateBugInput,
  User,
} from './types'

const BASE_URL = import.meta.env.VITE_API_URL

/** Every failed call throws this. `status` 0 means "could not reach the server at all". */
export class ApiError extends Error {
  status: number
  fieldErrors: FieldErrors

  constructor(status: number, message: string, fieldErrors: FieldErrors = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

// ---------------------------------------------------------------- hooks into the React app
interface ApiHandlers {
  getToken: () => string | null
  onUnauthorized: () => void
  onForbidden: (message: string) => void
}

let handlers: ApiHandlers = {
  getToken: () => null,
  onUnauthorized: () => {},
  onForbidden: () => {},
}

/** Called once by AuthProvider so this module can read the token without importing React. */
export function configureApi(next: Partial<ApiHandlers>) {
  handlers = { ...handlers, ...next }
}

// ---------------------------------------------------------------- core request function
interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  query?: Record<string, string | number | undefined>
  signal?: AbortSignal
  /** Login uses this: a 401 there means "wrong password", NOT "your session expired". */
  skipSessionHandling?: boolean
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: FieldErrors
}

function buildUrl(path: string, query?: RequestOptions['query']) {
  const url = new URL(path, BASE_URL)
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== '') {
      url.searchParams.set(key, String(value))
    }
  }
  return url.toString()
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, query, signal, skipSessionHandling = false } = options

  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  const token = handlers.getToken()
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }

  let response: Response
  try {
    response = await fetch(buildUrl(path, query), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error // a cancelled request is not a failure; callers ignore it
    }
    throw new ApiError(0, 'Cannot reach the server. Check your connection and try again.')
  }

  if (response.ok) {
    return (response.status === 204 ? undefined : await response.json()) as T
  }

  // ---- error path
  let problem: ProblemDetails = {}
  try {
    problem = (await response.json()) as ProblemDetails
  } catch {
    // body was empty or not JSON; fall back to generic messages below
  }

  const message = problem.detail ?? problem.title ?? `Request failed (${response.status}).`
  const error = new ApiError(response.status, message, problem.errors ?? {})

  if (response.status === 401 && !skipSessionHandling) {
    handlers.onUnauthorized()
  } else if (response.status === 403) {
    handlers.onForbidden("You don't have permission to do that.")
  }
  throw error
}

// ---------------------------------------------------------------- the API, one function per endpoint
export const api = {
  login: (email: string, password: string) =>
    request<LoginResponse>('/api/auth/login', {
      method: 'POST',
      body: { email, password },
      skipSessionHandling: true,
    }),

  getMe: () => request<User>('/api/auth/me'),

  getUsers: (signal?: AbortSignal) => request<User[]>('/api/users', { signal }),

  getBugs: (params: BugListParams, signal?: AbortSignal) =>
    request<PagedResult<Bug>>('/api/bugs', { query: { ...params }, signal }),

  getBug: (id: string | number, signal?: AbortSignal) =>
    request<Bug>(`/api/bugs/${encodeURIComponent(String(id))}`, { signal }),

  createBug: (input: CreateBugInput) => request<Bug>('/api/bugs', { method: 'POST', body: input }),

  updateBug: (id: number, input: UpdateBugInput) =>
    request<Bug>(`/api/bugs/${id}`, { method: 'PUT', body: input }),

  deleteBug: (id: number) => request<void>(`/api/bugs/${id}`, { method: 'DELETE' }),
}
