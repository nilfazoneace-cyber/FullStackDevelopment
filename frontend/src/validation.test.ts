import { describe, expect, it } from 'vitest'
import { validateBugForm, type BugFormValues } from './validation'

const valid: BugFormValues = { title: 'A valid title', description: '', severity: 'Low', status: 'Open', assignedToId: '' }

describe('validateBugForm', () => {
  it('accepts a valid form', () => {
    expect(validateBugForm(valid)).toEqual({})
  })

  it('rejects titles shorter than 5 characters (ignoring surrounding spaces)', () => {
    expect(validateBugForm({ ...valid, title: 'abcd' }).title).toMatch(/at least 5/)
    expect(validateBugForm({ ...valid, title: '  ab  ' }).title).toMatch(/at least 5/)
    expect(validateBugForm({ ...valid, title: 'abcde' }).title).toBeUndefined()
  })

  it('rejects titles longer than 120 characters', () => {
    expect(validateBugForm({ ...valid, title: 'x'.repeat(121) }).title).toMatch(/at most 120/)
    expect(validateBugForm({ ...valid, title: 'x'.repeat(120) }).title).toBeUndefined()
  })

  it('rejects descriptions longer than 2000 characters', () => {
    expect(validateBugForm({ ...valid, description: 'x'.repeat(2001) }).description).toMatch(/at most 2000/)
    expect(validateBugForm({ ...valid, description: 'x'.repeat(2000) }).description).toBeUndefined()
  })

  it('requires a severity', () => {
    expect(validateBugForm({ ...valid, severity: '' }).severity).toBeDefined()
  })
})
