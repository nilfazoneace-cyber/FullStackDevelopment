import { useCallback, useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { SeverityBadge, StatusBadge } from '../components/Badge'
import { statusLabel } from '../labels'
import { useAsync } from '../hooks/useAsync'
import { SEVERITIES, STATUSES, type BugStatus, type Severity } from '../types'

const PAGE_SIZE = 10

/**
 * Filters, search text and page number live in the URL (?status=Open&page=2) instead of component
 * state. That makes the view bookmarkable/shareable and the Back button behave naturally.
 */
export function BugListPage() {
  const [params, setParams] = useSearchParams()

  const status = (params.get('status') ?? '') as BugStatus | ''
  const severity = (params.get('severity') ?? '') as Severity | ''
  const search = params.get('search') ?? ''
  const page = Math.max(1, Number(params.get('page')) || 1)

  const updateParams = useCallback(
    (changes: Record<string, string>) => {
      setParams((prev) => {
        const next = new URLSearchParams(prev)
        for (const [key, value] of Object.entries(changes)) {
          if (value) next.set(key, value)
          else next.delete(key)
        }
        return next
      })
    },
    [setParams],
  )

  // The text box updates instantly; the URL (and therefore the API call) updates 300 ms after
  // the user stops typing, so we do not fire one request per keystroke ("debouncing").
  const [searchText, setSearchText] = useState(search)
  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (searchText.trim() !== search) updateParams({ search: searchText.trim(), page: '' })
    }, 300)
    return () => window.clearTimeout(timer)
  }, [searchText, search, updateParams])

  const { state, reload } = useAsync(
    (signal) => api.getBugs({ status, severity, search, page, pageSize: PAGE_SIZE }, signal),
    [status, severity, search, page],
  )

  const totalPages = state.status === 'success' ? Math.max(1, Math.ceil(state.data.total / PAGE_SIZE)) : 1

  // If the current page no longer exists (e.g. the last item on it was deleted), step back.
  useEffect(() => {
    if (state.status === 'success' && state.data.items.length === 0 && page > totalPages) {
      updateParams({ page: totalPages > 1 ? String(totalPages) : '' })
    }
  }, [state, page, totalPages, updateParams])

  const hasFilters = Boolean(status || severity || search)

  function clearFilters() {
    setSearchText('')
    setParams({})
  }

  return (
    <section>
      <div className="page-head">
        <h1>Bugs</h1>
        <Link to="/bugs/new" className="button primary">
          + New bug
        </Link>
      </div>

      <div className="filters card" role="search">
        <div>
          <label htmlFor="f-search">Search title</label>
          <input
            id="f-search"
            type="search"
            placeholder="e.g. checkout"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
          />
        </div>
        <div>
          <label htmlFor="f-status">Status</label>
          <select id="f-status" value={status} onChange={(e) => updateParams({ status: e.target.value, page: '' })}>
            <option value="">All</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {statusLabel(s)}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="f-severity">Severity</label>
          <select id="f-severity" value={severity} onChange={(e) => updateParams({ severity: e.target.value, page: '' })}>
            <option value="">All</option>
            {SEVERITIES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </div>
        {hasFilters && (
          <button type="button" onClick={clearFilters}>
            Clear filters
          </button>
        )}
      </div>

      {state.status === 'loading' && (
        <p role="status" className="muted pad">
          Loading bugs…
        </p>
      )}

      {state.status === 'error' && (
        <div className="banner banner-error" role="alert">
          <span>Could not load bugs: {state.error.message}</span>
          <button type="button" onClick={reload}>
            Try again
          </button>
        </div>
      )}

      {state.status === 'success' && state.data.items.length === 0 && (
        <div className="card empty">
          <p>{hasFilters ? 'No bugs match your filters.' : 'No bugs yet. Report the first one!'}</p>
          {hasFilters ? (
            <button type="button" onClick={clearFilters}>
              Clear filters
            </button>
          ) : (
            <Link to="/bugs/new" className="button primary">
              + New bug
            </Link>
          )}
        </div>
      )}

      {state.status === 'success' && state.data.items.length > 0 && (
        <>
          <div className="card table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Title</th>
                  <th>Severity</th>
                  <th>Status</th>
                  <th>Assignee</th>
                  <th>Updated</th>
                </tr>
              </thead>
              <tbody>
                {state.data.items.map((bug) => (
                  <tr key={bug.id}>
                    <td>
                      <Link to={`/bugs/${bug.id}`}>{bug.title}</Link>
                    </td>
                    <td>
                      <SeverityBadge severity={bug.severity} />
                    </td>
                    <td>
                      <StatusBadge status={bug.status} />
                    </td>
                    <td>{bug.assignedToName ?? <span className="muted">Unassigned</span>}</td>
                    <td>{new Date(bug.updatedAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <nav className="pager" aria-label="Pagination">
            <button type="button" disabled={page <= 1} onClick={() => updateParams({ page: String(page - 1) })}>
              ← Previous
            </button>
            <span>
              Page {page} of {totalPages} · {state.data.total} bug{state.data.total === 1 ? '' : 's'}
            </span>
            <button type="button" disabled={page >= totalPages} onClick={() => updateParams({ page: String(page + 1) })}>
              Next →
            </button>
          </nav>
        </>
      )}
    </section>
  )
}
