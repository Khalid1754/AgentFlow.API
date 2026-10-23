using System.ComponentModel;
using AgentFlow.Api.Services;

namespace AgentFlow.Api.Agents;

public record Finding(
    string Point,
    [property: Description("Numren på källorna som stödjer punkten, t.ex. [1, 3]. Tom lista om det är allmän kunskap.")] int[] Sources);

public record Analysis(
    [property: Description("Två till tre meningar som sammanfattar svaret på frågan")] string Summary,
    Finding[] Findings,
    [property: Description("Sådant källorna inte täcker eller som är osäkert")] string[] Gaps);

// Analyst: läser källorna och plockar ut fakta, jämförelser och luckor.
public class Analyst(ILlm llm)
{
    public const string Name = "Analyst";

    private const string System = """
        You are Analyst in a team of AI agents. Researcher has collected numbered sources. Extract the
        facts that answer the question, compare where relevant, and note gaps or uncertainty.
        Cite source numbers for each finding. Never invent facts that contradict the sources; if the
        sources are thin, you may add well-known general knowledge but cite no source for it.
        Write in the same language as the question.
        """;

    public Task<Analysis> AnalyzeAsync(string question, IReadOnlyList<Source> sources, CancellationToken ct) =>
        llm.CompleteJsonAsync<Analysis>(System, $"""
            Question: {question}

            Sources:
            {Researcher.Format(sources)}
            """, ct);

    public static string Format(Analysis analysis)
    {
        var findings = analysis.Findings.Select(f =>
            $"- {f.Point}{(f.Sources.Length > 0 ? " " + string.Concat(f.Sources.Select(n => $"[{n}]")) : "")}");
        var gaps = analysis.Gaps.Select(g => $"- {g}");
        return $"""
            **Sammanfattning:** {analysis.Summary}

            **Slutsatser**
            {string.Join("\n", findings)}

            **Luckor**
            {(analysis.Gaps.Length > 0 ? string.Join("\n", gaps) : "- Inga")}
            """;
    }
}
