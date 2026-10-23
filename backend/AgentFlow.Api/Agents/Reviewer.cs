using System.ComponentModel;
using AgentFlow.Api.Services;

namespace AgentFlow.Api.Agents;

public record Review(
    bool Approved,
    [property: Description("Konkreta saker Writer ska rätta. Tom sträng om rapporten är godkänd.")] string Feedback);

// Reviewer: granskar rapporten mot frågan och analysen och kan skicka tillbaka den till Writer.
public class Reviewer(ILlm llm)
{
    public const string Name = "Reviewer";

    private const string System = """
        You are Reviewer in a team of AI agents. Check that the report answers the question, matches
        the analysis, cites sources correctly and is well structured. Approve it if it is good enough
        to hand to the user; only reject it for real problems (missing parts of the question, claims
        not supported by the analysis, broken citations). Write feedback in the question's language.
        """;

    public Task<Review> ReviewAsync(string question, Analysis analysis, string report, CancellationToken ct) =>
        llm.CompleteJsonAsync<Review>(System, $"""
            Question: {question}

            Analysis:
            {Analyst.Format(analysis)}

            Report:
            {report}
            """, ct);
}
