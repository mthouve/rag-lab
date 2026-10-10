using Npgsql;

namespace RagLab.Infrastructure;

internal static class LocalDatabaseConfiguration
{
    public static string? ReadConnectionString(string contentRoot)
    {
        var envPath = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", ".env"));
        if (!File.Exists(envPath))
        {
            return null;
        }

        var entries = File.ReadLines(envPath)
            .Where(line => line.StartsWith("POSTGRES_PASSWORD=", StringComparison.Ordinal))
            .ToArray();
        if (entries.Length != 1)
        {
            throw new InvalidOperationException("Expected exactly one POSTGRES_PASSWORD entry in the local .env file.");
        }

        var password = entries[0]["POSTGRES_PASSWORD=".Length..];
        // This intentionally supports the generated local password, rather than interpreting dotenv expressions.
        if (string.IsNullOrWhiteSpace(password) || password.Any(character =>
            char.IsWhiteSpace(character) || character is '\'' or '"' or '$' or '#'))
        {
            throw new InvalidOperationException(
                "Local .env credentials require a plain, unquoted password without whitespace, quotes, $ or #. " +
                "For other passwords, configure ConnectionStrings:Postgres explicitly.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 5432,
            Database = "raglab",
            Username = "raglab",
            Password = password
        }.ConnectionString;
    }
}
