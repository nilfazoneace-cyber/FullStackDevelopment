import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { fakeApi, json, lead, makeBug, renderApp, requestsMade, signIn, tester } from './test/helpers'

// These tests render the REAL app (router + providers + pages + api.ts). Only the network is faked.

describe('authentication', () => {
  it('sends signed-out users to /login', async () => {
    fakeApi()
    renderApp('/')

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Bugs' })).not.toBeInTheDocument()
  })

  it('disables the submit button while the login request is running', async () => {
    let finishLogin!: () => void
    const gate = new Promise<void>((resolve) => (finishLogin = resolve))
    fakeApi({
      'POST /api/auth/login': async () => {
        await gate // hold the response until the test says so
        return json({ accessToken: 't', expiresAt: new Date(Date.now() + 3_600_000).toISOString(), user: tester })
      },
    })
    const user = renderApp('/')

    await signIn(user)

    const button = await screen.findByRole('button', { name: 'Signing in…' })
    expect(button).toBeDisabled()
    finishLogin()
    expect(await screen.findByRole('heading', { name: 'Bugs' })).toBeInTheDocument()
  })

  it('shows ONE generic error on 401 and lets the user retry', async () => {
    fakeApi({ 'POST /api/auth/login': () => json({ detail: 'Invalid email or password.' }, 401) })
    const user = renderApp('/')

    await signIn(user)

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled()
  })

  it('does not call the API when email or password is empty', async () => {
    const fetchMock = fakeApi()
    const user = renderApp('/')

    await user.click(await screen.findByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/enter your email and password/i)
    expect(requestsMade(fetchMock)).toEqual([])
  })

  it('keeps the token in memory only and sends it on API calls', async () => {
    const fetchMock = fakeApi()
    const user = renderApp('/')

    await signIn(user)
    await screen.findByRole('heading', { name: 'Bugs' })

    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
    const bugsCall = fetchMock.mock.calls.find(([url]) => new URL(String(url)).pathname === '/api/bugs')!
    expect(new Headers(bugsCall[1]?.headers).get('Authorization')).toBe('Bearer token-for-1')
  })

  it('returns to login with a message when ANY call answers 401', async () => {
    fakeApi({ 'GET /api/bugs': () => json({ title: 'Unauthorized' }, 401) })
    const user = renderApp('/')

    await signIn(user)

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent(/session has expired/i)
  })

  it('logout clears the session and protects the pages again', async () => {
    fakeApi()
    const user = renderApp('/')
    await signIn(user)
    await screen.findByRole('heading', { name: 'Bugs' })

    await user.click(screen.getByRole('button', { name: 'Log out' }))

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByTestId('current-user')).not.toBeInTheDocument()
  })

  it('returns the user to the page they originally asked for after login', async () => {
    fakeApi()
    const user = renderApp('/bugs/1')

    await signIn(user)

    expect(await screen.findByRole('heading', { name: 'Bug #1' })).toBeInTheDocument()
  })
})

describe('bug list', () => {
  it('shows a loading state, then the rows', async () => {
    fakeApi()
    const user = renderApp('/')
    await signIn(user)

    expect(await screen.findByText('Loading bugs…')).toBeInTheDocument()
    const link = await screen.findByRole('link', { name: 'Checkout total ignores discount' })
    const row = link.closest('tr')!
    expect(within(row).getByText('High')).toBeInTheDocument()
    expect(within(row).getByText('Open')).toBeInTheDocument()
    expect(screen.getByText('Lena Lead')).toBeInTheDocument() // assignee column
    expect(screen.getByText(/Page 1 of 1 · 2 bugs/)).toBeInTheDocument()
  })

  it('shows an empty state when there are no bugs', async () => {
    fakeApi({ 'GET /api/bugs': () => json({ items: [], total: 0, page: 1, pageSize: 10 }) })
    const user = renderApp('/')
    await signIn(user)

    expect(await screen.findByText(/No bugs yet/)).toBeInTheDocument()
  })

  it('shows an error state with a working "Try again" button', async () => {
    let calls = 0
    fakeApi({
      'GET /api/bugs': () =>
        ++calls === 1
          ? json({ title: 'An unexpected error occurred.' }, 500)
          : json({ items: [makeBug()], total: 1, page: 1, pageSize: 10 }),
    })
    const user = renderApp('/')
    await signIn(user)

    expect(await screen.findByRole('alert')).toHaveTextContent(/Could not load bugs/)
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('link', { name: 'Checkout total ignores discount' })).toBeInTheDocument()
  })

  it('sends the chosen filters and page to the API', async () => {
    const fetchMock = fakeApi()
    const user = renderApp('/')
    await signIn(user)
    await screen.findByRole('link', { name: 'Typo on pricing page' })

    await user.selectOptions(screen.getByLabelText('Status'), 'InProgress')
    await user.selectOptions(screen.getByLabelText('Severity'), 'Critical')

    await waitFor(() => {
      const last = new URL(String(fetchMock.mock.calls.at(-1)![0]))
      expect(last.searchParams.get('status')).toBe('InProgress')
      expect(last.searchParams.get('severity')).toBe('Critical')
    })
  })

  it('searches by title after the user stops typing', async () => {
    const fetchMock = fakeApi()
    const user = renderApp('/')
    await signIn(user)
    await screen.findByRole('link', { name: 'Typo on pricing page' })

    await user.type(screen.getByLabelText('Search title'), 'pricing')

    await waitFor(() => {
      const last = new URL(String(fetchMock.mock.calls.at(-1)![0]))
      expect(last.searchParams.get('search')).toBe('pricing')
    })
  })
})

describe('bug form', () => {
  it('validates on the client and does not call the API when invalid', async () => {
    const fetchMock = fakeApi()
    const user = renderApp('/bugs/new')
    await signIn(user)

    await user.type(await screen.findByLabelText('Title'), 'abc')
    await user.click(screen.getByRole('button', { name: 'Create bug' }))

    expect(await screen.findByText(/Title must be at least 5/)).toBeInTheDocument()
    expect(screen.getByText('Please choose a severity.')).toBeInTheDocument()
    expect(requestsMade(fetchMock)).not.toContain('POST /api/bugs')
  })

  it('goes to the new bug page after a 201', async () => {
    const fetchMock = fakeApi({
      'POST /api/bugs': () => json(makeBug({ id: 7, title: 'Brand new bug' }), 201),
      'GET /api/bugs/7': () => json(makeBug({ id: 7, title: 'Brand new bug' })),
    })
    const user = renderApp('/bugs/new')
    await signIn(user)

    await user.type(await screen.findByLabelText('Title'), 'Brand new bug')
    await user.selectOptions(screen.getByLabelText('Severity'), 'High')
    await user.click(screen.getByRole('button', { name: 'Create bug' }))

    expect(await screen.findByRole('heading', { name: 'Bug #7' })).toBeInTheDocument()
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST' && String(init?.body).includes('Brand new bug'))
    expect(JSON.parse(String(post![1]!.body))).toMatchObject({ title: 'Brand new bug', severity: 'High', assignedToId: null })
  })

  it('shows server 400 errors next to the right field', async () => {
    fakeApi({
      'POST /api/bugs': () =>
        json({ title: 'One or more validation errors occurred.', errors: { assignedToId: ['The selected assignee does not exist.'] } }, 400),
    })
    const user = renderApp('/bugs/new')
    await signIn(user)

    await user.type(await screen.findByLabelText('Title'), 'A perfectly valid title')
    await user.selectOptions(screen.getByLabelText('Severity'), 'Low')
    await user.click(screen.getByRole('button', { name: 'Create bug' }))

    const message = await screen.findByText('The selected assignee does not exist.')
    expect(message).toHaveAttribute('id', 'assignee-error')
    expect(screen.getByLabelText('Assignee')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Title')).toHaveAttribute('aria-invalid', 'false')
  })

  it('shows a clear message on 409 Conflict', async () => {
    fakeApi({
      'PUT /api/bugs/1': () => json({ title: 'Conflict', detail: 'This bug is Closed and can no longer be edited.' }, 409),
    })
    const user = renderApp('/bugs/1')
    await signIn(user)

    await user.click(await screen.findByRole('button', { name: 'Save changes' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('This bug is Closed and can no longer be edited.')
  })

  it('shows a permission message on 403 and stays on the same page', async () => {
    fakeApi({ 'PUT /api/bugs/1': () => json({ title: 'Forbidden' }, 403) })
    const user = renderApp('/bugs/1')
    await signIn(user)

    await user.click(await screen.findByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText("You don't have permission to do that.")).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Bug #1' })).toBeInTheDocument()
  })
})

describe('lead-only actions', () => {
  it('are hidden from Testers', async () => {
    fakeApi()
    const user = renderApp('/bugs/1')
    await signIn(user, tester)

    await screen.findByRole('heading', { name: 'Bug #1' })
    expect(screen.queryByRole('button', { name: 'Close bug' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete bug' })).not.toBeInTheDocument()
  })

  it('are shown to Leads, and Delete needs a confirmation', async () => {
    const fetchMock = fakeApi({
      'GET /api/bugs': () => json({ items: [], total: 0, page: 1, pageSize: 10 }),
    })
    const user = renderApp('/bugs/1')
    await signIn(user, lead)

    await user.click(await screen.findByRole('button', { name: 'Delete bug' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(requestsMade(fetchMock)).not.toContain('DELETE /api/bugs/1') // nothing yet

    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(requestsMade(fetchMock)).not.toContain('DELETE /api/bugs/1') // cancelled

    await user.click(screen.getByRole('button', { name: 'Delete bug' }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Yes, delete' }))

    expect(await screen.findByRole('heading', { name: 'Bugs' })).toBeInTheDocument() // back on the list
    expect(requestsMade(fetchMock)).toContain('DELETE /api/bugs/1')
    expect(screen.getByText('Bug deleted.')).toBeInTheDocument()
  })

  it('Close bug sends status Closed and then disables itself', async () => {
    const fetchMock = fakeApi()
    const user = renderApp('/bugs/1')
    await signIn(user, lead)

    await user.click(await screen.findByRole('button', { name: 'Close bug' }))

    expect(await screen.findByText('Bug closed.')).toBeInTheDocument()
    const put = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')!
    expect(JSON.parse(String(put[1]!.body)).status).toBe('Closed')
    expect(screen.getByRole('button', { name: 'Close bug' })).toBeDisabled()
  })
})
