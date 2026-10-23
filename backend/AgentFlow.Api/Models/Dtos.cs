using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentFlow.Api.Models;

public record CreateRunRequest(string Question);

public record RunSummaryDto(Guid Id, string Question, RunStatus Status, DateTime CreatedAt, DateTime? FinishedAt);

public record StepDto(int Sequence, string Agent, string? ToAgent, StepKind Kind, string Summary, string? Detail, DateTime CreatedAt)
{
    public static StepDto From(AgentStep s) => new(s.Sequence, s.Agent, s.ToAgent, s.Kind, s.Summary, s.Detail, s.CreatedAt);
}

public record RunDto(Guid Id, string Question, RunStatus Status, string? Report, string? Error,
    DateTime CreatedAt, DateTime? FinishedAt, IReadOnlyList<StepDto> Steps)
{
    public static RunDto From(Run r) => new(r.Id, r.Question, r.Status, r.Report, r.Error, r.CreatedAt, r.FinishedAt,
        r.Steps.OrderBy(s => s.Sequence).Select(StepDto.From).ToList());
}

// Det som skickas live till webbläsaren: antingen ett nytt steg eller en ny status för körningen.
public record RunEvent(string Type, StepDto? Step = null, RunStatus? Status = null, string? Report = null, string? Error = null)
{
    public static RunEvent ForStep(AgentStep s) => new("step", Step: StepDto.From(s));
    public static RunEvent ForStatus(Run r) => new("status", Status: r.Status, Report: r.Report, Error: r.Error);
}

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions o)
    {
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        return o;
    }
}
