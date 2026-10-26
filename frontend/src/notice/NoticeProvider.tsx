import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { NoticeContext, type Notice } from './notice-context'

const INFO_AUTO_DISMISS_MS = 5000

/** A single app-wide banner message ("Session expired", "No permission", "Bug deleted"). */
export function NoticeProvider({ children }: { children: ReactNode }) {
  const [notice, setNotice] = useState<Notice | null>(null)

  const notify = useCallback((message: string, kind: Notice['kind'] = 'info') => {
    setNotice({ message, kind })
  }, [])
  const dismiss = useCallback(() => setNotice(null), [])

  // Good news fades away by itself; errors stay until the user closes them.
  useEffect(() => {
    if (notice?.kind !== 'info') return
    const timer = window.setTimeout(() => setNotice(null), INFO_AUTO_DISMISS_MS)
    return () => window.clearTimeout(timer)
  }, [notice])

  const value = useMemo(() => ({ notice, notify, dismiss }), [notice, notify, dismiss])
  return <NoticeContext.Provider value={value}>{children}</NoticeContext.Provider>
}
