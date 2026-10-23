import ReactMarkdown from 'react-markdown'

// Länkar i rapporter och källistor öppnas i en ny flik.
export default function Markdown({ children }: { children: string }) {
  return (
    <div className="markdown">
      <ReactMarkdown components={{ a: ({ href, children }) => <a href={href} target="_blank" rel="noreferrer">{children}</a> }}>
        {children}
      </ReactMarkdown>
    </div>
  )
}
