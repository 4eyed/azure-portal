using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MenuApi.Services;

public interface IJwtTokenValidator
{
    Task<ClaimsPrincipal?> ValidateTokenAsync(HttpRequest request);
}

public class JwtTokenValidator : IJwtTokenValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenValidator> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenValidator(IConfiguration configuration, ILogger<JwtTokenValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(HttpRequest request)
    {
        // Extract Bearer token from Authorization header
        if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return null;
        }

        var token = authHeader.ToString();
        if (string.IsNullOrEmpty(token) || !token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        token = token.Substring("Bearer ".Length).Trim();

        try
        {
            var tenantId = _configuration["AZURE_TENANT_ID"];
            var backendClientId = _configuration["AZURE_CLIENT_ID"];
            var frontendClientId = _configuration["AZURE_FRONTEND_CLIENT_ID"];

            if (string.IsNullOrEmpty(tenantId))
            {
                _logger.LogWarning("AZURE_TENANT_ID not configured");
                return null;
            }

            // Build list of valid audiences - include both frontend and backend client IDs
            var validAudiences = new List<string>();
            if (!string.IsNullOrEmpty(backendClientId))
                validAudiences.Add(backendClientId);
            if (!string.IsNullOrEmpty(frontendClientId))
                validAudiences.Add(frontendClientId);

            if (validAudiences.Count == 0)
            {
                _logger.LogWarning("No valid audience configured (AZURE_CLIENT_ID or AZURE_FRONTEND_CLIENT_ID)");
                return null;
            }

            // Get OpenID Connect configuration for token validation
            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            var discoveryDocument = await configurationManager.GetConfigurationAsync();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = new[]
                {
                    $"https://login.microsoftonline.com/{tenantId}/v2.0",
                    $"https://sts.windows.net/{tenantId}/"
                },
                ValidateAudience = true,
                ValidAudiences = validAudiences,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = discoveryDocument.SigningKeys,
                RoleClaimType = "roles" // Map roles claim type
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);

            _logger.LogInformation("Token validated successfully. User: {User}, Claims: {ClaimCount}",
                principal.Identity?.Name ?? "Unknown",
                principal.Claims.Count());

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return null;
        }
    }
}
