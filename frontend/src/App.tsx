import { useCallback, useEffect, useState } from 'react'
import { api, type RunSummary } from './api'
import NewRun from './components/NewRun'
import RunList from './components/RunList'
import RunView from './components/RunView'

export default function App() {
  const [runs, setRuns] = useState<RunSummary[]>([])
  const [selected, setSelected] = useState<string | null>(null)
  const [aiEnabled, setAiEnabled] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(() => {
    api.listRuns().then(setRuns, e => setError((e as Error).message))
  }, [])

  useEffect(() => {
    refresh()
    api.status().then(s => setAiEnabled(s.aiEnabled), () => {})
  }, [refresh])

  async function create(question: string) {
    const run = await api.createRun(question)
    setRuns(r => [run, ...r])
    setSelected(run.id)
  }

  async function remove(id: string) {
    await api.deleteRun(id)
    setSelected(null)
    refresh()
  }

  const current = runs.find(r => r.id === selected)

  return (
    <>
      <header>
        <h1>Agent<span>Flow</span></h1>
        <span className="muted">Fyra AI-agenter som lämnar över till varandra</span>
      </header>
      {!aiEnabled && <div className="banner">GROQ_API_KEY saknas. Lägg in den i .env och starta om med start.bat.</div>}
      {error && <div className="banner">{error}</div>}
      <main>
        <aside>
          <NewRun onCreate={create} disabled={!aiEnabled} />
          <h3>Tidigare körningar</h3>
          <RunList runs={runs} selected={selected} onSelect={setSelected} />
        </aside>
        <div>
          {current ? (
            <RunView key={current.id} run={current} onFinished={refresh} onDelete={() => remove(current.id)} />
          ) : (
            <div className="card intro">
              <h2>Så funkar det</h2>
              <ol>
                <li><b>🔎 Researcher</b> söker på Wikipedia och DuckDuckGo och samlar källor.</li>
                <li><b>📊 Analyst</b> plockar ut fakta och hittar luckor.</li>
                <li><b>✍️ Writer</b> skriver en rapport med källhänvisningar.</li>
                <li><b>✅ Reviewer</b> granskar och kan skicka tillbaka rapporten till Writer.</li>
              </ol>
              <p className="muted">Skriv en fråga till vänster så ser du överlämningarna live.</p>
            </div>
          )}
        </div>
      </main>
    </>
  )
}
