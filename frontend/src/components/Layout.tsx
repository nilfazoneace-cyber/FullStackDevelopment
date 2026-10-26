import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/auth-context'

/** The frame around every signed-in page: header with navigation, user name and Logout. */
export function Layout() {
  const { user, logout } = useAuth()

  return (
    <>
      <header className="app-header">
        <Link to="/" className="brand">
          🐞 BugBoard
        </Link>
        <nav aria-label="Main">
          <Link to="/">Bugs</Link>
          <Link to="/bugs/new">New bug</Link>
        </nav>
        <div className="user-box">
          <span data-testid="current-user">
            {user?.displayName} <span className="badge role">{user?.role}</span>
          </span>
          <button type="button" onClick={logout}>
            Log out
          </button>
        </div>
      </header>
      <main className="container">
        <Outlet />
      </main>
    </>
  )
}
