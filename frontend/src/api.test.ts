import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api, configureApi } from './api'
import { fakeApi, json } from './test/helpers'

const onUnauthorized = vi.fn()
const onForbidden = vi.fn()

beforeEach(() => {
  onUnauthorized.mockReset()
  onForbidden.mockReset()
  configureApi({ getToken: () => 'my-jwt', onUnauthorized, onForbidden })
})

describe('api client', () => {
  it('adds the Authorization: Bearer header to every request', async () => {
    const fetchMock = fakeApi()

    await api.getUsers()

    const headers = new Headers(fetchMock.mock.calls[0][1]?.headers)
    expect(headers.get('Authorization')).toBe('Bearer my-jwt')
  })

  it('sends no Authorization header when signed out', async () => {
    configureApi({ getToken: () => null })
    const fetchMock = fakeApi()

    await api.getUsers()

    const headers = new Headers(fetchMock.mock.calls[0][1]?.headers)
    expect(headers.has('Authorization')).toBe(false)
  })

  it('uses VITE_API_URL as the base and leaves empty filters out of the query string', async () => {
    const fetchMock = fakeApi()

    await api.getBugs({ status: '', severity: 'High', search: '', page: 2, pageSize: 10 })

    const url = new URL(String(fetchMock.mock.calls[0][0]))
    expect(url.origin).toBe(new URL(import.meta.env.VITE_API_URL).origin)
    expect(url.pathname).toBe('/api/bugs')
    expect(url.searchParams.get('severity')).toBe('High')
    expect(url.searchParams.get('page')).toBe('2')
    expect(url.searchParams.has('status')).toBe(false)
    expect(url.searchParams.has('search')).toBe(false)
  })

  it('calls onUnauthorized on a 401 and still throws', async () => {
    fakeApi({ 'GET /api/users': () => json({ title: 'Unauthorized' }, 401) })

    await expect(api.getUsers()).rejects.toMatchObject({ status: 401 })
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })

  it('does NOT treat a 401 from login as an expired session', async () => {
    fakeApi({ 'POST /api/auth/login': () => json({ detail: 'Invalid email or password.' }, 401) })

    await expect(api.login('a@b.c', 'bad')).rejects.toMatchObject({ status: 401, message: 'Invalid email or password.' })
    expect(onUnauthorized).not.toHaveBeenCalled()
  })

  it('calls onForbidden on a 403', async () => {
    fakeApi({ 'DELETE /api/bugs/1': () => json({ title: 'Forbidden' }, 403) })

    await expect(api.deleteBug(1)).rejects.toMatchObject({ status: 403 })
    expect(onForbidden).toHaveBeenCalledOnce()
  })

  it('exposes per-field errors from a 400 ValidationProblemDetails', async () => {
    fakeApi({ 'POST /api/bugs': () => json({ title: 'One or more validation errors occurred.', errors: { title: ['Too short'] } }, 400) })

    const error = await api
      .createBug({ title: 'x', description: null, severity: 'Low', assignedToId: null })
      .catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(400)
    expect((error as ApiError).fieldErrors).toEqual({ title: ['Too short'] })
  })

  it('returns undefined for 204 No Content', async () => {
    fakeApi()
    await expect(api.deleteBug(1)).resolves.toBeUndefined()
  })

  it('turns a network failure into ApiError with status 0', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(api.getUsers()).rejects.toMatchObject({ status: 0 })
  })
})
