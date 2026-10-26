import { createContext, useContext } from 'react'

export interface Notice {
  kind: 'info' | 'error'
  message: string
}

export interface NoticeContextValue {
  notice: Notice | null
  notify: (message: string, kind?: Notice['kind']) => void
  dismiss: () => void
}

export const NoticeContext = createContext<NoticeContextValue | null>(null)

export function useNotice(): NoticeContextValue {
  const ctx = useContext(NoticeContext)
  if (!ctx) throw new Error('useNotice must be used inside <NoticeProvider>')
  return ctx
}
