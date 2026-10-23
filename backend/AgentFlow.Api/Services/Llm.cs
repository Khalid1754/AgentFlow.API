using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace AgentFlow.Api.Services;

public class LlmException(string message) : Exception(message);

// En språkmodell. Med ett JSON-schema svarar den med JSON som följer schemat, annars med fritext.
public interface ILlm
{
    bool Enabled { get; }
    Task<string> CompleteAsync(string system, string prompt, JsonObject? schema, CancellationToken ct);
}

public static class LlmExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    public static async Task<T> CompleteJsonAsync<T>(this ILlm llm, string system, string prompt, CancellationToken ct)
    {
        var text = await llm.CompleteAsync(system, prompt, SchemaFor<T>(), ct);
        try
        {
            return JsonSerializer.Deserialize<T>(text, Json) ?? throw new JsonException("Tomt svar");
        }
        catch (JsonException)
        {
            throw new LlmException("AI-svaret kunde inte tolkas.");
        }
    }

    // Bygger ett JSON-schema från C#-typen: alla fält obligatoriska och inga extra fält,
    // vilket är vad structured outputs (strict) kräver.
    public static JsonObject SchemaFor<T>()
    {
        var options = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, node) =>
            {
                if (node is not JsonObject obj) return node;
                var description = context.PropertyInfo?.AttributeProvider?
                    .GetCustomAttributes(typeof(DescriptionAttribute), inherit: false)
                    .OfType<DescriptionAttribute>().FirstOrDefault()?.Description;
                if (description is not null) obj["description"] = description;
                if (obj["properties"] is JsonObject props)
                {
                    obj["required"] = new JsonArray(props.Select(p => (JsonNode)p.Key).ToArray());
                    obj["additionalProperties"] = false;
                }
                return obj;
            },
        };
        return (JsonObject)Json.GetJsonSchemaAsNode(typeof(T), options);
    }
}

// Groq via det OpenAI-kompatibla REST-API:t. Snabbt och har en gratisnivå:
// skapa nyckel på https://console.groq.com/keys
public class GroqLlm(IConfiguration config, HttpClient http) : ILlm
{
    private readonly string? _apiKey = new[] { config["Groq:ApiKey"], Environment.GetEnvironmentVariable("GROQ_API_KEY") }
        .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    public bool Enabled => _apiKey is not null;

    public async Task<string> CompleteAsync(string system, string prompt, JsonObject? schema, CancellationToken ct)
    {
        if (!Enabled)
            throw new LlmException("GROQ_API_KEY saknas. Lägg in den i .env och starta om.");

        var baseUrl = (config["Groq:BaseUrl"] ?? "https://api.groq.com").TrimEnd('/');
        var body = new JsonObject
        {
            ["model"] = config["Groq:Model"] ?? "openai/gpt-oss-120b",
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = prompt }),
            ["reasoning_effort"] = config["Groq:ReasoningEffort"] ?? "low",
        };
        if (schema is not null)
        {
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject { ["name"] = "svar", ["strict"] = true, ["schema"] = schema.DeepClone() },
            };
        }

        // Gratiskvoten räknas per minut. Fyra agenter i rad kan slå i taket, så vid 429 väntar vi
        // det Groq säger (högst Groq:MaxRetryWaitSeconds) och försöker igen ett par gånger.
        for (var attempt = 1; ; attempt++)
        {
            var (response, text) = await SendAsync(baseUrl, body, ct);
            using var _ = response;
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt <= MaxRetries)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * attempt);
                var maxWait = TimeSpan.FromSeconds(config.GetValue("Groq:MaxRetryWaitSeconds", 30));
                await Task.Delay(wait < maxWait ? wait : maxWait, ct);
                continue;
            }
            return Parse(response, text);
        }
    }

    public const int MaxRetries = 2;

    private async Task<(HttpResponseMessage, string)> SendAsync(string baseUrl, JsonObject body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/openai/v1/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        try
        {
            var response = await http.SendAsync(request, ct);
            return (response, await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new LlmException("Kunde inte nå Groq. Kontrollera internetanslutningen och försök igen.");
        }
    }

    private static string Parse(HttpResponseMessage response, string text)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new LlmException(response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => "Groq: gratiskvoten är slut för stunden, försök igen om en minut.",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Ogiltig GROQ_API_KEY.",
                _ => $"Groq-fel ({(int)response.StatusCode}): {ErrorMessage(text)}",
            });
        }

        var choice = JsonNode.Parse(text)?["choices"]?[0];
        var content = choice?["message"]?["content"]?.GetValue<string>() ?? "";
        if (content.Length == 0)
            throw new LlmException($"Groq gav inget svar (finish_reason: {choice?["finish_reason"]?.GetValue<string>() ?? "okänd"}).");
        return content;
    }

    private static string ErrorMessage(string body)
    {
        try { return JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>() ?? body; }
        catch (JsonException) { return body; }
    }
}
