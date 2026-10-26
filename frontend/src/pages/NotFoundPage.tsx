import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="card empty">
      <h1>Page not found</h1>
      <p>The page you are looking for does not exist.</p>
      <Link to="/" className="button primary">
        Back to bugs
      </Link>
    </div>
  )
}
