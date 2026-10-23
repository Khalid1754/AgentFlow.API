import { useState, type FormEvent } from 'react'

const EXAMPLES = [
  'Jämför värmepump och fjärrvärme för en villa i Sverige',
  'Vad är skillnaden mellan kärnkraft och vindkraft när det gäller kostnad och utsläpp?',
  'Sammanfatta historien bakom Öresundsbron',
]

export default function NewRun({ onCreate, disabled }: { onCreate: (question: string) => Promise<void>; disabled: boolean }) {
  const [question, setQuestion] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await onCreate(question)
      setQuestion('')
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit}>
      <label>
        Vad vill du att agenterna tar reda på?
        <textarea rows={3} maxLength={500} value={question} onChange={e => setQuestion(e.target.value)}
          placeholder="Skriv en fråga…" />
      </label>
      <div className="examples">
        {EXAMPLES.map(example => (
          <button type="button" key={example} className="chip" onClick={() => setQuestion(example)}>{example}</button>
        ))}
      </div>
      <button disabled={busy || disabled || question.trim().length < 5}>{busy ? 'Startar…' : 'Starta agenterna'}</button>
      {error && <p className="error">{error}</p>}
    </form>
  )
}
