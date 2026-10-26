import type { BugStatus, Severity } from './types'

export const TITLE_MIN = 5
export const TITLE_MAX = 120
export const DESCRIPTION_MAX = 2000

export interface BugFormValues {
  title: string
  description: string
  severity: Severity | ''
  status: BugStatus
  assignedToId: string // '' = unassigned (HTML <select> values are always strings)
}

/** Field name -> single message. Empty object = valid. */
export type FormErrors = Partial<Record<keyof BugFormValues, string>>

/**
 * Client-side validation. It mirrors the API rules ONLY to give instant feedback.
 * The API validates again (never trust the client), and its answer wins.
 */
export function validateBugForm(values: BugFormValues): FormErrors {
  const errors: FormErrors = {}

  if (values.title.trim().length < TITLE_MIN) {
    errors.title = `Title must be at least ${TITLE_MIN} characters.`
  } else if (values.title.length > TITLE_MAX) {
    errors.title = `Title must be at most ${TITLE_MAX} characters.`
  }

  if (values.description.length > DESCRIPTION_MAX) {
    errors.description = `Description must be at most ${DESCRIPTION_MAX} characters.`
  }

  if (values.severity === '') {
    errors.severity = 'Please choose a severity.'
  }

  return errors
}
