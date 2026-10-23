import { describe, expect, it } from 'vitest'
import type { Step } from './api'
import { agentStates } from './agents'
import { applyEvent, emptyRun } from './runState'

const step = (sequence: number, agent: string, kind: Step['kind'], toAgent?: string): Step =>
  ({ sequence, agent, kind, toAgent, summary: '', createdAt: '' })

describe('applyEvent', () => {
  it('sorterar steg och hoppar över dubbletter', () => {
    let state = applyEvent(emptyRun, { type: 'step', step: step(2, 'Researcher', 'completed') })
    state = applyEvent(state, { type: 'step', step: step(1, 'Researcher', 'started') })
    state = applyEvent(state, { type: 'step', step: step(2, 'Researcher', 'completed') })
    expect(state.steps.map(s => s.sequence)).toEqual([1, 2])
  })

  it('sparar status och rapport', () => {
    const state = applyEvent(emptyRun, { type: 'status', status: 'completed', report: '# Klar' })
    expect(state).toMatchObject({ status: 'completed', report: '# Klar' })
  })
})

describe('agentStates', () => {
  it('visar vem som jobbar och vem som är klar', () => {
    const states = agentStates([
      step(1, 'Researcher', 'started'),
      step(2, 'Researcher', 'completed'),
      step(3, 'Researcher', 'handoff', 'Analyst'),
      step(4, 'Analyst', 'started'),
    ])
    expect(states).toEqual({ Researcher: 'done', Analyst: 'working', Writer: 'waiting', Reviewer: 'waiting' })
  })

  it('Writer jobbar igen när Reviewer skickar tillbaka rapporten', () => {
    const states = agentStates([
      step(1, 'Writer', 'completed'),
      step(2, 'Reviewer', 'handoff', 'Writer'),
      step(3, 'Writer', 'started'),
    ])
    expect(states.Writer).toBe('working')
    expect(states.Reviewer).toBe('done')
  })
})
