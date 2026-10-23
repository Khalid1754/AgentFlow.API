using AgentFlow.Api.Services;

namespace AgentFlow.Api.Agents;

// Writer: skriver rapporten i Markdown utifrån analysen, och skriver om den när Reviewer har synpunkter.
public class Writer(ILlm llm)
{
    public const string Name = "Writer";

    private const string System = """
        You are Writer in a team of AI agents. Write a clear, well-structured report in Markdown that
        answers the question, based on Analyst's findings. Start with a # title and a short summary,
        use ## sections, cite sources inline as [1], [2] and end with a "## Källor" (or "## Sources"
        in English) list of the cited sources with links. Write in the same language as the question.
        Output only the report.
        """;

    public async Task<string> WriteAsync(string question, Analysis analysis, IReadOnlyList<Source> sources,
        string? previousReport, string? feedback, CancellationToken ct)
    {
        var revision = feedback is null ? "" : $"""

            Your previous draft:
            {previousReport}

            Reviewer's feedback, fix all of it:
            {feedback}
            """;
        var report = await llm.CompleteAsync(System, $"""
            Question: {question}

            Analysis:
            {Analyst.Format(analysis)}

            Sources:
            {Researcher.Format(sources)}
            {revision}
            """, null, ct);
        return report.Trim();
    }
}
