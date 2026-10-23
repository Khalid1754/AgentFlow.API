import type { RunEvent, RunStatus, Step } from './api'

export interface RunState {
  steps: Step[]
  status: RunStatus | null
  report?: string
  error?: string
}

export const emptyRun: RunState = { steps: [], status: null }

export const isFinished = (status: RunStatus | null) => status === 'completed' || status === 'failed'

// Lägger in en händelse från servern. Steg kan komma två gånger om anslutningen återupptas.
export function applyEvent(state: RunState, event: RunEvent): RunState {
  if (event.type === 'step') {
    if (state.steps.some(s => s.sequence === event.step.sequence)) return state
    const steps = [...state.steps, event.step].sort((a, b) => a.sequence - b.sequence)
    return { ...state, steps }
  }
  return { ...state, status: event.status, report: event.report, error: event.error }
}
