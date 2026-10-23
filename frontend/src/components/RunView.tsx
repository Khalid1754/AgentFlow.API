import { useState } from 'react'
import type { RunSummary } from '../api'
import { useRunStream } from '../useRunStream'
import Markdown from './Markdown'
import Pipeline from './Pipeline'
import Timeline from './Timeline'

export default function RunView({ run, onFinished, onDelete }: { run: RunSummary; onFinished: () => void; onDelete: () => void }) {
  const state = useRunStream(run.id, onFinished)
  const [copied, setCopied] = useState(false)

  async function copy() {
    await navigator.clipboard.writeText(state.report ?? '')
    setCopied(true)
    setTimeout(() => setCopied(false), 1500)
  }

  function download() {
    const url = URL.createObjectURL(new Blob([state.report ?? ''], { type: 'text/markdown' }))
    const link = Object.assign(document.createElement('a'), { href: url, download: 'rapport.md' })
    link.click()
    URL.revokeObjectURL(url)
  }

  return (
    <section>
      <div className="card">
        <h2>{run.question}</h2>
        {state.status === 'queued' && <p className="muted">I kö, startar strax…</p>}
        <Pipeline steps={state.steps} />
      </div>

      {state.error && <div className="card error">{state.error}</div>}

      {state.report && (
        <div className="card report">
          <div className="actions">
            <button className="secondary" onClick={copy}>{copied ? 'Kopierad!' : 'Kopiera'}</button>
            <button className="secondary" onClick={download}>Ladda ner .md</button>
          </div>
          <Markdown>{state.report}</Markdown>
        </div>
      )}

      {state.steps.length > 0 && (
        <div className="card">
          <h3>Vad agenterna gjorde</h3>
          <Timeline steps={state.steps} />
        </div>
      )}

      {(state.status === 'completed' || state.status === 'failed') && (
        <button className="danger" onClick={onDelete}>Ta bort körningen</button>
      )}
    </section>
  )
}
