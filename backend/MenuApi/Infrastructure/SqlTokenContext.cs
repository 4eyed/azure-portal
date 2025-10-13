using System;
using System.Threading;

namespace MenuApi.Infrastructure;

/// <summary>
/// Provides per-request storage for delegated SQL access tokens using <see cref="AsyncLocal{T}"/>.
/// </summary>
public static class SqlTokenContext
{
    private static readonly AsyncLocal<string?> Current = new();

    /// <summary>
    /// Gets the delegated SQL token associated with the current async flow.
    /// </summary>
    public static string? CurrentToken => Current.Value;

    /// <summary>
    /// Sets the delegated SQL token for the current async flow and returns a scope that will
    /// automatically restore the previous value when disposed.
    /// </summary>
    /// <param name="token">The delegated SQL access token, if available.</param>
    /// <returns>An <see cref="IDisposable"/> scope that restores the previous token value.</returns>
    public static IDisposable BeginScope(string? token)
    {
        var previous = Current.Value;
        Current.Value = token;
        return new SqlTokenScope(previous);
    }

    private sealed class SqlTokenScope : IDisposable
    {
        private readonly string? _previousToken;
        private bool _disposed;

        public SqlTokenScope(string? previousToken)
        {
            _previousToken = previousToken;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Current.Value = _previousToken;
            _disposed = true;
        }
    }
}
