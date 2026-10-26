import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './auth-context'

/**
 * Wraps every page that needs a signed-in user. Signed out -> /login, remembering where the
 * user wanted to go so login can send them back. This is UX only; the API enforces real security.
 */
export function ProtectedRoute() {
  const { user } = useAuth()
  const location = useLocation()

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }
  return <Outlet />
}
