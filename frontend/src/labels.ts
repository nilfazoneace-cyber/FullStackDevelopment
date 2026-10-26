import type { BugStatus } from './types'

const STATUS_LABELS: Record<BugStatus, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  Resolved: 'Resolved',
  Closed: 'Closed',
}

/** "InProgress" (what the API sends) -> "In progress" (what humans read). */
export function statusLabel(status: BugStatus) {
  return STATUS_LABELS[status]
}
