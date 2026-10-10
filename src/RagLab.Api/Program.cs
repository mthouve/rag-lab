using System.Net;
using Npgsql;
using RagLab.Application;
using RagLab.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5080");
builder.Logging.AddJsonConsole();
var options = builder.Configuration.GetSection("Rag").Get<RagOptions>() ?? new();
options.Validate();
options.AllowedRoot = Path.GetFullPath(options.AllowedRoot, builder.Environment.ContentRootPath);
var connection = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(connection) && builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("LocalDevelopment:UseComposeCredentials"))
{
    connection = LocalDatabaseConfiguration.ReadConnectionString(builder.Environment.ContentRootPath);
}
if (string.IsNullOrWhiteSpace(connection))
{
    throw new InvalidOperationException("Set ConnectionStrings__Postgres or the ConnectionStrings:Postgres user secret.");
}

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
builder.Services.AddSingleton<PostgresDocumentStore>();
builder.Services.AddSingleton<IDocumentStore>(sp => sp.GetRequiredService<PostgresDocumentStore>());
builder.Services.AddHttpClient<OllamaProvider>(http =>
{
    http.BaseAddress = new Uri(options.OllamaUrl);
    http.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddSingleton<IEmbeddingProvider>(sp => sp.GetRequiredService<OllamaProvider>());
builder.Services.AddSingleton<IChatProvider>(sp => sp.GetRequiredService<OllamaProvider>());
builder.Services.AddSingleton<TextChunker>();
builder.Services.AddSingleton<RagService>();
var app = builder.Build();
await app.Services.GetRequiredService<PostgresDocumentStore>().InitializeAsync(app.Lifetime.ApplicationStopping);
app.Use(async (context, next) =>
{
    // Stage 1 is unauthenticated and local only. No CORS; reject browser cross-origin mutations.
    if (context.Connection.RemoteIpAddress is { } remote && !IPAddress.IsLoopback(remote))
    {
        context.Response.StatusCode = 403;
        return;
    }

    if (context.Request.Method == "POST")
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && origin != $"{context.Request.Scheme}://{context.Request.Host}")
        {
            context.Response.StatusCode = 403;
            return;
        }

        if (!context.Request.HasJsonContentType())
        {
            context.Response.StatusCode = 415;
            return;
        }
    }

    try
    {
        await next(context);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        context.Response.StatusCode = 499;
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Request failed: {Method} {Path}", context.Request.Method, context.Request.Path);
        var status = ex switch
        {
            ArgumentException => 400,
            FileNotFoundException => 404,
            HttpRequestException => 503,
            NpgsqlException => 503,
            OperationCanceledException => 504,
            _ => 500
        };
        await Results.Problem(
            statusCode: status,
            title: status == 400 || status == 404 ? ex.Message : "The operation failed. Check PostgreSQL, Ollama, and the server logs.").ExecuteAsync(context);
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapPost(
    "/api/documents/index",
    async (IndexRequest request, RagService rag, CancellationToken ct) => Results.Ok(await rag.IndexAsync(request.Path, ct)));
app.MapGet(
    "/api/documents",
    async (IDocumentStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(ct)));
app.MapPost(
    "/api/ask",
    async (QuestionRequest request, RagService rag, CancellationToken ct) => Results.Ok(await rag.AskAsync(request.Question, ct)));
app.MapGet(
    "/health",
    async (NpgsqlDataSource db, CancellationToken ct) =>
{
    await using var connection = await db.OpenConnectionAsync(ct);
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT 1";
    await command.ExecuteScalarAsync(ct);
    return Results.Ok(new
    {
        status = "ready",
        database = "connected"
    });
});
app.Run();
public record IndexRequest(string Path);
public record QuestionRequest(string Question);
public partial class Program;
