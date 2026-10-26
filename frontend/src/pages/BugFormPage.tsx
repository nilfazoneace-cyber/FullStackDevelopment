import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError, api } from '../api'
import { StatusBadge } from '../components/Badge'
import { statusLabel } from '../labels'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { useAuth } from '../auth/auth-context'
import { useAsync } from '../hooks/useAsync'
import { useNotice } from '../notice/notice-context'
import { SEVERITIES, STATUSES, type Bug, type BugStatus, type CreateBugInput, type Severity, type User } from '../types'
import {
  DESCRIPTION_MAX,
  TITLE_MAX,
  validateBugForm,
  type BugFormValues,
  type FormErrors,
} from '../validation'

const FIELDS: (keyof BugFormValues)[] = ['title', 'description', 'severity', 'status', 'assignedToId']

/**
 * Route component for BOTH "/bugs/new" and "/bugs/:id".
 * It only loads data and shows loading / error states; the form itself is <BugForm>.
 */
export function BugFormPage() {
  const { id } = useParams()
  const isNew = id === undefined

  const users = useAsync((signal) => api.getUsers(signal), [])
  const bug = useAsync((signal) => (isNew ? Promise.resolve(null) : api.getBug(id, signal)), [id])

  if (users.state.status === 'loading' || bug.state.status === 'loading') {
    return (
      <p role="status" className="muted pad">
        Loading…
      </p>
    )
  }

  if (bug.state.status === 'error') {
    const notFound = bug.state.error.status === 404
    return (
      <div className="banner banner-error" role="alert">
        <span>{notFound ? 'This bug does not exist (it may have been deleted).' : `Could not load the bug: ${bug.state.error.message}`}</span>
        {notFound ? <Link to="/">Back to the list</Link> : <button type="button" onClick={bug.reload}>Try again</button>}
      </div>
    )
  }

  if (users.state.status === 'error') {
    return (
      <div className="banner banner-error" role="alert">
        <span>Could not load users: {users.state.error.message}</span>
        <button type="button" onClick={users.reload}>
          Try again
        </button>
      </div>
    )
  }

  return <BugForm key={bug.state.data?.id ?? 'new'} initialBug={bug.state.data} users={users.state.data} />
}

// ====================================================================================================

function toFormValues(bug: Bug | null): BugFormValues {
  return {
    title: bug?.title ?? '',
    description: bug?.description ?? '',
    severity: bug?.severity ?? '',
    status: bug?.status ?? 'Open',
    assignedToId: bug?.assignedToId?.toString() ?? '',
  }
}

function toInput(values: BugFormValues): CreateBugInput {
  return {
    title: values.title.trim(),
    description: values.description.trim() || null,
    severity: values.severity as Severity, // validated as non-empty before we get here
    assignedToId: values.assignedToId ? Number(values.assignedToId) : null,
  }
}

/** Server error keys ("title", "assignedToId", ...) -> our form fields. Unknown keys are returned separately. */
function mapServerErrors(fieldErrors: Record<string, string[]>) {
  const known: FormErrors = {}
  const unknown: string[] = []
  for (const [key, messages] of Object.entries(fieldErrors)) {
    const field = FIELDS.find((f) => f.toLowerCase() === key.toLowerCase())
    if (field) known[field] = messages.join(' ')
    else unknown.push(...messages)
  }
  return { known, unknown }
}

interface BugFormProps {
  initialBug: Bug | null // null = creating a new bug
  users: User[]
}

function BugForm({ initialBug, users }: BugFormProps) {
  const { user } = useAuth()
  const { notify } = useNotice()
  const navigate = useNavigate()

  const isNew = initialBug === null
  const isLead = user?.role === 'Lead'

  const [saved, setSaved] = useState<Bug | null>(initialBug) // last version the server confirmed
  const [values, setValues] = useState<BugFormValues>(() => toFormValues(initialBug))
  const [errors, setErrors] = useState<FormErrors>({})
  const [otherErrors, setOtherErrors] = useState<string[]>([])
  const [formError, setFormError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  function setField<K extends keyof BugFormValues>(field: K, value: BugFormValues[K]) {
    setValues((v) => ({ ...v, [field]: value }))
    setErrors((e) => ({ ...e, [field]: undefined })) // typing in a field clears its old error
    setSuccess(null)
  }

  function resetMessages() {
    setErrors({})
    setOtherErrors([])
    setFormError(null)
    setSuccess(null)
  }

  /** Turn whatever the API said into the right place on screen. */
  function showApiError(error: unknown) {
    if (!(error instanceof ApiError)) {
      setFormError('Something went wrong. Please try again.')
      return
    }
    switch (error.status) {
      case 400: {
        const { known, unknown } = mapServerErrors(error.fieldErrors)
        setErrors(known) // field-level messages appear next to the right input
        setOtherErrors(unknown)
        if (Object.keys(known).length === 0 && unknown.length === 0) setFormError(error.message)
        break
      }
      case 401: // AuthProvider already cleared the session and ProtectedRoute redirects to /login
      case 403: // the app-wide banner already says "You don't have permission"
        break
      case 409:
        setFormError(error.message) // e.g. "This bug is Closed and can no longer be edited."
        break
      default:
        setFormError(error.message)
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (busy) return
    resetMessages()

    const clientErrors = validateBugForm(values) // fast feedback, no network call
    if (Object.keys(clientErrors).length > 0) {
      setErrors(clientErrors)
      return
    }

    setBusy(true)
    try {
      if (isNew) {
        const created = await api.createBug(toInput(values))
        notify('Bug created.')
        navigate(`/bugs/${created.id}`) // the assignment: on 201, go to the new bug's page
        return
      }
      const updated = await api.updateBug(saved!.id, { ...toInput(values), status: values.status })
      setSaved(updated)
      setValues(toFormValues(updated))
      setSuccess('Changes saved.')
    } catch (e) {
      showApiError(e)
    } finally {
      setBusy(false)
    }
  }

  async function handleClose() {
    if (!saved || busy) return
    resetMessages()
    setBusy(true)
    try {
      // The API has no "close" endpoint: closing is an update with status = Closed (Lead only, server-checked).
      const updated = await api.updateBug(saved.id, {
        title: saved.title,
        description: saved.description,
        severity: saved.severity,
        assignedToId: saved.assignedToId,
        status: 'Closed',
      })
      setSaved(updated)
      setValues(toFormValues(updated))
      setSuccess('Bug closed.')
    } catch (e) {
      showApiError(e)
    } finally {
      setBusy(false)
    }
  }

  async function handleDelete() {
    if (!saved) return
    setBusy(true)
    try {
      await api.deleteBug(saved.id)
      notify('Bug deleted.')
      navigate('/')
    } catch (e) {
      setConfirmingDelete(false)
      showApiError(e)
      setBusy(false)
    }
  }

  // Statuses offered in the dropdown. "Closed" is only offered to Leads: pure UX, the API enforces it.
  const statusOptions = STATUSES.filter((s) => s !== 'Closed' || isLead || saved?.status === 'Closed')

  const notReporter = !isNew && !isLead && saved!.reportedById !== user?.id
  const isClosed = saved?.status === 'Closed'

  return (
    <section>
      <div className="page-head">
        <h1>{isNew ? 'New bug' : `Bug #${saved!.id}`}</h1>
        {!isNew && <StatusBadge status={saved!.status} />}
      </div>

      {!isNew && (
        <p className="muted">
          Reported by {saved!.reportedByName} on {new Date(saved!.createdAt).toLocaleString()} · last updated{' '}
          {new Date(saved!.updatedAt).toLocaleString()}
        </p>
      )}

      {isClosed && (
        <div className="banner banner-warn" role="status">
          This bug is Closed. Closed bugs cannot be edited, so the server will reject any change.
        </div>
      )}
      {notReporter && !isClosed && (
        <div className="banner banner-warn" role="status">
          You did not report this bug. Only its reporter or a Lead can change it, so the server will reject
          your changes.
        </div>
      )}

      <form className="card form" onSubmit={handleSubmit} noValidate aria-label={isNew ? 'New bug' : 'Edit bug'}>
        {formError && (
          <div className="banner banner-error" role="alert">
            {formError}
          </div>
        )}
        {otherErrors.length > 0 && (
          <div className="banner banner-error" role="alert">
            {otherErrors.join(' ')}
          </div>
        )}
        {success && (
          <div className="banner banner-info" role="status">
            {success}
          </div>
        )}

        <label htmlFor="title">Title</label>
        <input
          id="title"
          value={values.title}
          onChange={(e) => setField('title', e.target.value)}
          aria-invalid={Boolean(errors.title)}
          aria-describedby={errors.title ? 'title-error' : undefined}
        />
        <small className="muted">
          {values.title.length}/{TITLE_MAX}
        </small>
        {errors.title && <FieldError id="title-error" message={errors.title} />}

        <label htmlFor="description">Description</label>
        <textarea
          id="description"
          rows={5}
          value={values.description}
          onChange={(e) => setField('description', e.target.value)}
          aria-invalid={Boolean(errors.description)}
          aria-describedby={errors.description ? 'description-error' : undefined}
        />
        <small className="muted">
          {values.description.length}/{DESCRIPTION_MAX}
        </small>
        {errors.description && <FieldError id="description-error" message={errors.description} />}

        <div className="row">
          <div>
            <label htmlFor="severity">Severity</label>
            <select
              id="severity"
              value={values.severity}
              onChange={(e) => setField('severity', e.target.value as Severity | '')}
              aria-invalid={Boolean(errors.severity)}
              aria-describedby={errors.severity ? 'severity-error' : undefined}
            >
              <option value="">Select…</option>
              {SEVERITIES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
            {errors.severity && <FieldError id="severity-error" message={errors.severity} />}
          </div>

          {!isNew && (
            <div>
              <label htmlFor="status">Status</label>
              <select
                id="status"
                value={values.status}
                onChange={(e) => setField('status', e.target.value as BugStatus)}
                aria-invalid={Boolean(errors.status)}
              >
                {statusOptions.map((s) => (
                  <option key={s} value={s}>
                    {statusLabel(s)}
                  </option>
                ))}
              </select>
              {errors.status && <FieldError id="status-error" message={errors.status} />}
            </div>
          )}

          <div>
            <label htmlFor="assignee">Assignee</label>
            <select
              id="assignee"
              value={values.assignedToId}
              onChange={(e) => setField('assignedToId', e.target.value)}
              aria-invalid={Boolean(errors.assignedToId)}
              aria-describedby={errors.assignedToId ? 'assignee-error' : undefined}
            >
              <option value="">Unassigned</option>
              {users.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.displayName} ({u.role})
                </option>
              ))}
            </select>
            {errors.assignedToId && <FieldError id="assignee-error" message={errors.assignedToId} />}
          </div>
        </div>

        <div className="actions">
          <button type="submit" className="primary" disabled={busy}>
            {busy ? 'Saving…' : isNew ? 'Create bug' : 'Save changes'}
          </button>
          <Link to="/" className="button">
            Back to list
          </Link>

          {/* Lead-only actions. Hidden from Testers as UX; the API rejects them with 403 anyway. */}
          {!isNew && isLead && (
            <span className="lead-actions">
              <button type="button" onClick={handleClose} disabled={busy || isClosed}>
                Close bug
              </button>
              <button type="button" className="danger" onClick={() => setConfirmingDelete(true)} disabled={busy}>
                Delete bug
              </button>
            </span>
          )}
        </div>
      </form>

      {confirmingDelete && (
        <ConfirmDialog
          title="Delete this bug?"
          message={`"${saved!.title}" will be permanently deleted. This cannot be undone.`}
          confirmLabel="Yes, delete"
          busy={busy}
          onConfirm={handleDelete}
          onCancel={() => setConfirmingDelete(false)}
        />
      )}
    </section>
  )
}

function FieldError({ id, message }: { id: string; message: string }) {
  return (
    <p id={id} className="field-error" role="alert">
      {message}
    </p>
  )
}
