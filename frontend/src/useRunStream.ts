import { useEffect, useReducer, useRef } from 'react'
import { api, type RunEvent } from './api'
import { applyEvent, emptyRun, isFinished, type RunState } from './runState'

type Action = { type: 'reset' } | { type: 'event'; event: RunEvent }

const reducer = (state: RunState, action: Action) => (action.type === 'reset' ? emptyRun : applyEvent(state, action.event))

// Följer en körning live via Server-Sent Events.
export function useRunStream(runId: string | null, onFinished?: () => void): RunState {
  const [state, dispatch] = useReducer(reducer, emptyRun)
  const finished = useRef(onFinished)
  useEffect(() => { finished.current = onFinished })

  useEffect(() => {
    dispatch({ type: 'reset' })
    if (!runId) return
    const source = new EventSource(api.eventsUrl(runId))
    const handle = (e: MessageEvent<string>) => {
      const event = JSON.parse(e.data) as RunEvent
      dispatch({ type: 'event', event })
      // Servern stänger strömmen när körningen är klar; stäng här så att webbläsaren inte ansluter igen.
      if (event.type === 'status' && isFinished(event.status)) {
        source.close()
        finished.current?.()
      }
    }
    source.addEventListener('step', handle)
    source.addEventListener('status', handle)
    return () => source.close()
  }, [runId])

  return state
}
