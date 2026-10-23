import type { Step } from './api'

export const AGENTS = [
  { name: 'Researcher', icon: '🔎', label: 'Söker information' },
  { name: 'Analyst', icon: '📊', label: 'Analyserar' },
  { name: 'Writer', icon: '✍️', label: 'Skriver rapport' },
  { name: 'Reviewer', icon: '✅', label: 'Granskar' },
] as const

export type AgentState = 'waiting' | 'working' | 'done' | 'failed'

export const iconFor = (agent: string) => AGENTS.find(a => a.name === agent)?.icon ?? '🤖'

// En agents läge avgörs av det senaste steget den själv gjorde.
export function agentStates(steps: Step[]): Record<string, AgentState> {
  const states: Record<string, AgentState> = Object.fromEntries(AGENTS.map(a => [a.name, 'waiting']))
  for (const step of [...steps].sort((a, b) => a.sequence - b.sequence)) {
    states[step.agent] = step.kind === 'started' ? 'working' : step.kind === 'failed' ? 'failed' : 'done'
  }
  return states
}
