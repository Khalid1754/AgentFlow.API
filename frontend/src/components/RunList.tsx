import type { RunStatus, RunSummary } from '../api'

const STATUS: Record<RunStatus, string> = { queued: 'I kö', running: 'Pågår', completed: 'Klar', failed: 'Misslyckades' }

export default function RunList({ runs, selected, onSelect }: { runs: RunSummary[]; selected: string | null; onSelect: (id: string) => void }) {
  if (runs.length === 0) return <p className="muted">Inga körningar än.</p>
  return (
    <ul className="runs">
      {runs.map(run => (
        <li key={run.id}>
          <button className={run.id === selected ? 'active' : ''} onClick={() => onSelect(run.id)}>
            <span className="question">{run.question}</span>
            <span className={`badge ${run.status}`}>{STATUS[run.status]}</span>
          </button>
        </li>
      ))}
    </ul>
  )
}
