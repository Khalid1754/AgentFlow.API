using System.Net;
using System.Text.Json.Nodes;
using AgentFlow.Api.Agents;
using AgentFlow.Api.Services;
using Microsoft.Extensions.Configuration;

namespace AgentFlow.Api.Tests;

public class GroqLlmTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public async Task SendsStrictSchemaAndKey_ParsesJson()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            {"choices":[{"message":{"role":"assistant","content":"{\"approved\":true,\"feedback\":\"\"}"},"finish_reason":"stop"}]}
            """);
        var llm = new GroqLlm(Config(new() { ["Groq:ApiKey"] = "test-key" }), new HttpClient(handler));

        var review = await llm.CompleteJsonAsync<Review>("system", "prompt", CancellationToken.None);

        Assert.True(review.Approved);
        Assert.Equal("/openai/v1/chat/completions", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer test-key", handler.Request.Headers.Authorization!.ToString());
        var body = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal("openai/gpt-oss-120b", body["model"]!.GetValue<string>());
        var format = body["response_format"]!["json_schema"]!;
        Assert.True(format["strict"]!.GetValue<bool>());
        Assert.False(format["schema"]!["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(2, format["schema"]!["required"]!.AsArray().Count);
    }

    [Fact]
    public async Task PlainText_SendsNoResponseFormat()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"choices":[{"message":{"content":"# Rapport"}}]}""");
        var llm = new GroqLlm(Config(new() { ["Groq:ApiKey"] = "k" }), new HttpClient(handler));

        Assert.Equal("# Rapport", await llm.CompleteAsync("s", "p", null, CancellationToken.None));
        Assert.Null(JsonNode.Parse(handler.RequestBody!)!["response_format"]);
    }

    [Fact]
    public async Task RateLimit_IsRetriedBeforeGivingUp()
    {
        var handler = new FakeHandler(HttpStatusCode.TooManyRequests, """{"error":{"message":"Rate limit"}}""");
        var llm = new GroqLlm(Config(new() { ["Groq:ApiKey"] = "k", ["Groq:MaxRetryWaitSeconds"] = "0" }), new HttpClient(handler));

        await Assert.ThrowsAsync<LlmException>(() => llm.CompleteAsync("s", "p", null, CancellationToken.None));
        Assert.Equal(1 + GroqLlm.MaxRetries, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Ogiltig GROQ_API_KEY.")]
    [InlineData(HttpStatusCode.TooManyRequests, "Groq: gratiskvoten är slut för stunden, försök igen om en minut.")]
    public async Task Errors_GiveSwedishMessages(HttpStatusCode status, string expected)
    {
        var llm = new GroqLlm(Config(new() { ["Groq:ApiKey"] = "k", ["Groq:MaxRetryWaitSeconds"] = "0" }),
            new HttpClient(new FakeHandler(status, """{"error":{"message":"x"}}""")));
        var error = await Assert.ThrowsAsync<LlmException>(() => llm.CompleteAsync("s", "p", null, CancellationToken.None));
        Assert.Equal(expected, error.Message);
    }

    [Fact]
    public async Task MissingKey_Throws()
    {
        var llm = new GroqLlm(Config(new()), new HttpClient());
        if (llm.Enabled) return; // GROQ_API_KEY är satt i miljön
        await Assert.ThrowsAsync<LlmException>(() => llm.CompleteAsync("s", "p", null, CancellationToken.None));
    }
}
