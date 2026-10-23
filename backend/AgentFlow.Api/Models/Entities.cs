namespace AgentFlow.Api.Models;

public enum RunStatus { Queued, Running, Completed, Failed }

// Vad som hände i ett steg: en agent börjar, blir klar, lämnar över till nästa eller misslyckas.
public enum StepKind { Started, Completed, Handoff, Failed }

// En körning = en fråga som agenterna tillsammans besvarar med en rapport.
public class Run
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Question { get; set; } = "";
    public RunStatus Status { get; set; } = RunStatus.Queued;
    public string? Report { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public List<AgentStep> Steps { get; set; } = [];
}

public class AgentStep
{
    public int Id { get; set; }
    public Guid RunId { get; set; }
    public int Sequence { get; set; }
    public string Agent { get; set; } = "";
    public string? ToAgent { get; set; }
    public StepKind Kind { get; set; }
    public string Summary { get; set; } = "";
    // Det agenten producerade (Markdown), visas när man fäller ut steget.
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
