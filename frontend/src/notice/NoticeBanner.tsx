import { useNotice } from './notice-context'

export function NoticeBanner() {
  const { notice, dismiss } = useNotice()
  if (!notice) return null

  return (
    <div className={`banner banner-${notice.kind}`} role={notice.kind === 'error' ? 'alert' : 'status'}>
      <span>{notice.message}</span>
      <button type="button" className="link-button" onClick={dismiss} aria-label="Dismiss message">
        ×
      </button>
    </div>
  )
}
