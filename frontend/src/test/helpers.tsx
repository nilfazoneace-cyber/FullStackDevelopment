import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import App from '../App'
import { AuthProvider } from '../auth/AuthProvider'
import { NoticeProvider } from '../notice/NoticeProvider'
import type { Bug, User } from '../types'

// ---------------------------------------------------------------- fixtures
export const tester: User = { id: 1, email: 'tester1@bugboard.local', displayName: 'Tina Tester', role: 'Tester' }
export const lead: User = { id: 3, email: 'lead@bugboard.local', displayName: 'Lena Lead', role: 'Lead' }

export function makeBug(overrides: Partial<Bug> = {}): Bug {
  return {
    id: 1,
    title: 'Checkout total ignores discount',
    description: 'Discount code is accepted but total is unchanged.',
    severity: 'High',
    status: 'Open',
    reportedById: 1,
    reportedByName: 'Tina Tester',
    assignedToId: null,
    assignedToName: null,
    createdAt: '2026-10-20T08:00:00Z',
    updatedAt: '2026-10-21T09:30:00Z',
    ...overrides,
  }
}

// ---------------------------------------------------------------- fake API (replaces window.fetch)
export function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
  })
}

export interface FakeRequest {
  method: string
  url: URL
  headers: Headers
  body: unknown
}
export type FakeHandler = (req: FakeRequest) => Response | Promise<Response>

/** Routes are keyed "METHOD /path". Anything not overridden gets a sensible default. */
export function fakeApi(overrides: Record<string, FakeHandler> = {}) {
  const defaults: Record<string, FakeHandler> = {
    'POST /api/auth/login': (req) => {
      const email = (req.body as { email: string }).email
      const user = email === lead.email ? lead : tester
      return json({ accessToken: `token-for-${user.id}`, expiresAt: new Date(Date.now() + 3_600_000).toISOString(), user })
    },
    'GET /api/users': () => json([tester, lead]),
    'GET /api/bugs': () => json({ items: [makeBug(), makeBug({ id: 2, title: 'Typo on pricing page', severity: 'Low', assignedToId: 3, assignedToName: 'Lena Lead' })], total: 2, page: 1, pageSize: 10 }),
    'GET /api/bugs/1': () => json(makeBug()),
    'PUT /api/bugs/1': (req) => json(makeBug({ ...(req.body as Partial<Bug>), id: 1 })),
    'DELETE /api/bugs/1': () => new Response(null, { status: 204 }),
  }
  const routes = { ...defaults, ...overrides }

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input))
    const method = init?.method ?? 'GET'
    const handler = routes[`${method} ${url.pathname}`]
    if (!handler) return json({ title: `No fake route for ${method} ${url.pathname}` }, 404)
    return handler({
      method,
      url,
      headers: new Headers(init?.headers),
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
    })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

/** All requests the app made, as "METHOD /path" strings, e.g. to assert "no DELETE was sent". */
export function requestsMade(fetchMock: ReturnType<typeof fakeApi>) {
  return fetchMock.mock.calls.map(([input, init]) => `${init?.method ?? 'GET'} ${new URL(String(input)).pathname}`)
}

// ---------------------------------------------------------------- rendering
export function renderApp(path = '/') {
  const user = userEvent.setup()
  render(
    <MemoryRouter initialEntries={[path]}>
      <NoticeProvider>
        <AuthProvider>
          <App />
        </AuthProvider>
      </NoticeProvider>
    </MemoryRouter>,
  )
  return user
}

export async function signIn(user: ReturnType<typeof userEvent.setup>, who: User = tester) {
  await user.type(screen.getByLabelText('Email'), who.email)
  await user.type(screen.getByLabelText('Password'), 'any-password')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
}
