import { Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { Layout } from './components/Layout'
import { NoticeBanner } from './notice/NoticeBanner'
import { BugFormPage } from './pages/BugFormPage'
import { BugListPage } from './pages/BugListPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'

/** The route table: which URL shows which screen, and which ones need a signed-in user. */
export default function App() {
  return (
    <>
      <NoticeBanner />
      <Routes>
        <Route path="/login" element={<LoginPage />} />

        {/* Everything inside here requires a signed-in user */}
        <Route element={<ProtectedRoute />}>
          <Route element={<Layout />}>
            <Route index element={<BugListPage />} />
            <Route path="bugs/new" element={<BugFormPage />} />
            <Route path="bugs/:id" element={<BugFormPage />} />
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Route>
      </Routes>
    </>
  )
}
