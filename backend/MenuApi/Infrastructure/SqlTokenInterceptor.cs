using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MenuApi.Infrastructure;

/// <summary>
/// Applies the delegated SQL access token to outgoing connections when available.
/// </summary>
public class SqlTokenInterceptor : DbConnectionInterceptor
{
    private readonly ILogger<SqlTokenInterceptor> _logger;

    public SqlTokenInterceptor(ILogger<SqlTokenInterceptor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        ApplyDelegatedToken(connection);
        return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }

    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        ApplyDelegatedToken(connection);
        return base.ConnectionOpening(connection, eventData, result);
    }

    private void ApplyDelegatedToken(DbConnection connection)
    {
        if (connection is not SqlConnection sqlConnection)
        {
            return;
        }

        var token = SqlTokenContext.CurrentToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            sqlConnection.AccessToken = token;
            _logger.LogInformation(
                "Using delegated SQL token for connection (length: {Length}, preview: {Preview})",
                token.Length,
                GetPreview(token));
        }
        else
        {
            _logger.LogDebug("No delegated SQL token present; using configured authentication.");
        }
    }

    private static string GetPreview(string token)
    {
        const int previewLength = 12;
        return token.Length <= previewLength
            ? token
            : $"{token[..previewLength]}…";
    }
}
