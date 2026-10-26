import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api'
import { useAuth } from '../auth/auth-context'
import { useNotice } from '../notice/notice-context'

export function LoginPage() {
  const { user, login } = useAuth()
  const { dismiss } = useNotice()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Already signed in (e.g. pressed Back to /login): go straight to the app.
  if (user) {
    return <Navigate to={from} replace />
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault() // stop the browser's default full-page form submit
    if (submitting) return

    if (!email.trim() || !password) {
      setError('Please enter your email and password.')
      return
    }

    setError(null)
    setSubmitting(true)
    try {
      await login(email.trim(), password)
      dismiss() // clear any old "session expired" banner
      navigate(from, { replace: true })
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) {
        // One generic message: never say whether it was the email or the password.
        setError('Invalid email or password.')
      } else if (e instanceof ApiError) {
        setError(e.message)
      } else {
        setError('Something went wrong. Please try again.')
      }
      setSubmitting(false)
    }
  }

  return (
    <div className="login-wrap">
      <form className="card login-card" onSubmit={handleSubmit} noValidate>
        <h1>🐞 BugBoard</h1>
        <p className="muted">Sign in to continue</p>

        {error && (
          <div className="banner banner-error" role="alert">
            {error}
          </div>
        )}

        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          disabled={submitting}
        />

        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          disabled={submitting}
        />

        <button type="submit" className="primary" disabled={submitting}>
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}
