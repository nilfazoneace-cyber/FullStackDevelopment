import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import './index.css'
import App from './App.tsx'
import { AuthProvider } from './auth/AuthProvider.tsx'
import { NoticeProvider } from './notice/NoticeProvider.tsx'

// Provider order matters: AuthProvider uses the notice system, so NoticeProvider is outside it.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <NoticeProvider>
        <AuthProvider>
          <App />
        </AuthProvider>
      </NoticeProvider>
    </BrowserRouter>
  </StrictMode>,
)
