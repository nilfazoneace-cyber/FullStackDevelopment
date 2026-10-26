import { statusLabel } from '../labels'
import type { BugStatus, Severity } from '../types'

export function SeverityBadge({ severity }: { severity: Severity }) {
  return <span className={`badge sev-${severity.toLowerCase()}`}>{severity}</span>
}

export function StatusBadge({ status }: { status: BugStatus }) {
  return <span className={`badge status-${status.toLowerCase()}`}>{statusLabel(status)}</span>
}
