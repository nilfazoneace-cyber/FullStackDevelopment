import { useCallback, useEffect, useState, type DependencyList } from 'react'
import { ApiError } from '../api'

export type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'error'; error: ApiError }
  | { status: 'success'; data: T }

/**
 * Runs an async loader whenever `deps` change and exposes loading / error / success.
 * Every screen that fetches data uses this, so all of them get the same three states.
 *
 * - The AbortSignal cancels the previous request when deps change or the component unmounts,
 *   which prevents an old, slow response from overwriting a newer one ("race condition").
 * - `reload()` re-runs the loader (used by "Try again" buttons).
 */
export function useAsync<T>(loader: (signal: AbortSignal) => Promise<T>, deps: DependencyList) {
  const [state, setState] = useState<AsyncState<T>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    let cancelled = false
    // Defer so the reset to "loading" is not a synchronous setState inside the effect body.
    queueMicrotask(() => {
      if (!cancelled) setState({ status: 'loading' })
    })

    loader(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) setState({ status: 'success', data })
      },
      (error: unknown) => {
        if (controller.signal.aborted) return
        setState({
          status: 'error',
          error: error instanceof ApiError ? error : new ApiError(0, 'Something went wrong.'),
        })
      },
    )

    return () => {
      cancelled = true
      controller.abort()
    }
    // `loader` is recreated on every render, so it is intentionally not a dependency: callers list
    // the values it actually uses in `deps`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, attempt])

  const reload = useCallback(() => setAttempt((n) => n + 1), [])
  return { state, reload }
}
