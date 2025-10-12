using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace MenuApi.Infrastructure;

/// <summary>
/// Interceptor that sets the SQL access token from the user's MSAL token
/// This allows SQL to authenticate as the actual end-user, not the backend service
/// Uses AsyncLocal storage since IHttpContextAccessor doesn't work in Azure Functions Isolated Worker
/// </summary>
public class SqlTokenInterceptor : DbConnectionInterceptor
{
    private readonly ILogger<SqlTokenInterceptor> _logger;

    public SqlTokenInterceptor(ILogger<SqlTokenInterceptor> logger)
    {
        _logger = logger;
    }

    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (connection is SqlConnection sqlConnection)
        {
            // Get SQL token from AsyncLocal context (set by middleware/function)
            var sqlToken = SqlTokenContext.SqlToken;

            if (!string.IsNullOrEmpty(sqlToken))
            {
                // Set AccessToken on SQL connection - this authenticates as the user
                sqlConnection.AccessToken = sqlToken;

                _logger.LogInformation(
                    "✅ SQL connection using user's access token (token: {TokenPrefix}...)",
                    sqlToken.Substring(0, Math.Min(20, sqlToken.Length))
                );
            }
            else
            {
                // In production (Azure), Managed Identity from connection string will be used
                // In local dev, this would be an error if no SQL token is provided
                _logger.LogDebug(
                    "No SQL token in SqlTokenContext - using connection string authentication (Managed Identity in Azure, or error in local dev)"
                );
            }
        }

        return await base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }
}
