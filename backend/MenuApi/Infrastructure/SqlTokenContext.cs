using System.Threading;

namespace MenuApi.Infrastructure;

/// <summary>
/// Provides access to the SQL access token for the current async context
/// Uses AsyncLocal to maintain token per request in Azure Functions Isolated Worker
/// </summary>
public static class SqlTokenContext
{
    private static readonly AsyncLocal<string?> _sqlToken = new AsyncLocal<string?>();

    /// <summary>
    /// Gets or sets the SQL access token for the current async context
    /// </summary>
    public static string? SqlToken
    {
        get => _sqlToken.Value;
        set => _sqlToken.Value = value;
    }
}
