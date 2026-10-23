using AgentFlow.Api.Agents;
using AgentFlow.Api.Data;
using AgentFlow.Api.Endpoints;
using AgentFlow.Api.Models;
using AgentFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AgentFlowDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("AgentFlow"), sql => sql.EnableRetryOnFailure()));
builder.Services.ConfigureHttpJsonOptions(o => ApiJson.Configure(o.SerializerOptions));

builder.Services.AddHttpClient<GroqLlm>(c => c.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddSingleton<ILlm>(sp => sp.GetRequiredService<GroqLlm>());

// Wikipedia kräver en User-Agent som säger vem som anropar.
builder.Services.AddHttpClient("search", c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("AgentFlow/1.0 (https://github.com/khalid1754/agentflow)");
});
foreach (var language in builder.Configuration.GetSection("Search:WikipediaLanguages").Get<string[]>() ?? ["sv", "en"])
    builder.Services.AddSingleton<ISearchSource>(sp => new WikipediaSearch(sp.GetRequiredService<IHttpClientFactory>().CreateClient("search"), language));
builder.Services.AddSingleton<ISearchSource>(sp => new DuckDuckGoSearch(sp.GetRequiredService<IHttpClientFactory>().CreateClient("search")));

builder.Services.AddSingleton<Researcher>();
builder.Services.AddSingleton<Analyst>();
builder.Services.AddSingleton<Writer>();
builder.Services.AddSingleton<Reviewer>();
builder.Services.AddSingleton<RunEvents>();
builder.Services.AddSingleton<RunQueue>();
builder.Services.AddSingleton<RunOrchestrator>();
builder.Services.AddHostedService<RunWorker>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await DatabaseStartup.MigrateAsync(app.Services, app.Logger);

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { error = "Något gick fel på servern." });
}));
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.MapRunEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

// Gör Program synlig för testerna (WebApplicationFactory).
public partial class Program;
