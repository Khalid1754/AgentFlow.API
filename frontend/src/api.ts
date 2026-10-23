export type RunStatus = 'queued' | 'running' | 'completed' | 'failed'
export type StepKind = 'started' | 'completed' | 'handoff' | 'failed'

export interface Step {
  sequence: number
  agent: string
  toAgent?: string
  kind: StepKind
  summary: string
  detail?: string
  createdAt: string
}

export interface RunSummary {
  id: string
  question: string
  status: RunStatus
  createdAt: string
  finishedAt?: string
}

export interface Run extends RunSummary {
  report?: string
  error?: string
  steps: Step[]
}

export type RunEvent =
  | { type: 'step'; step: Step }
  | { type: 'status'; status: RunStatus; report?: string; error?: string }

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.error ?? `Servern svarade ${response.status}`)
  }
  return response.status === 204 ? (undefined as T) : response.json()
}

export const api = {
  status: () => request<{ aiEnabled: boolean }>('/api/status'),
  listRuns: () => request<RunSummary[]>('/api/runs'),
  createRun: (question: string) => request<Run>('/api/runs', { method: 'POST', body: JSON.stringify({ question }) }),
  deleteRun: (id: string) => request<void>(`/api/runs/${id}`, { method: 'DELETE' }),
  eventsUrl: (id: string) => `/api/runs/${id}/events`,
}
