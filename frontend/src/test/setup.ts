import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

afterEach(() => {
  cleanup() // unmount rendered components so one test cannot affect the next
  vi.unstubAllGlobals() // restore the real fetch
})
