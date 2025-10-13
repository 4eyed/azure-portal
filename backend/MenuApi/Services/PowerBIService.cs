using Azure.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using MenuApi.Models.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MenuApi.Services;

/// <summary>
/// Implementation of Power BI service that authenticates using the Function App's managed identity
/// or other DefaultAzureCredential-supported identities.
/// </summary>
public class PowerBIService : IPowerBIService
{
    private static readonly Uri PowerBiApiRoot = new("https://api.powerbi.com/");
    private static readonly string[] PowerBiScopes = ["https://analysis.windows.net/powerbi/api/.default"];

    private readonly TokenCredential _credential;
    private readonly ILogger<PowerBIService> _logger;
    private readonly string? _overrideApiUrl;

    public PowerBIService(TokenCredential credential, ILogger<PowerBIService> logger, IConfiguration configuration)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _overrideApiUrl = configuration["POWERBI_API_URL"];
    }

    public async Task<List<PowerBIWorkspaceResponse>> GetWorkspacesAsync()
    {
        using var client = await CreateClientAsync();
        var groups = await client.Groups.GetGroupsAsync();

        return groups.Value.Select(g => new PowerBIWorkspaceResponse
        {
            Id = g.Id.ToString(),
            Name = g.Name
        }).ToList();
    }

    public async Task<List<PowerBIReportResponse>> GetReportsAsync(string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new ArgumentException("Workspace ID is required", nameof(workspaceId));
        }

        using var client = await CreateClientAsync();
        var reports = await client.Reports.GetReportsInGroupAsync(Guid.Parse(workspaceId));

        return reports.Value.Select(r => new PowerBIReportResponse
        {
            Id = r.Id.ToString(),
            Name = r.Name,
            EmbedUrl = r.EmbedUrl
        }).ToList();
    }

    public async Task<EmbedTokenResponse> GenerateEmbedTokenAsync(string workspaceId, string reportId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(reportId))
        {
            throw new ArgumentException("WorkspaceId and ReportId are required to generate an embed token.");
        }

        using var client = await CreateClientAsync();
        var generateTokenRequest = new GenerateTokenRequest(accessLevel: "View");

        var embedToken = await client.Reports.GenerateTokenInGroupAsync(
            Guid.Parse(workspaceId),
            Guid.Parse(reportId),
            generateTokenRequest);

        return new EmbedTokenResponse
        {
            Token = embedToken.Token,
            Expiration = embedToken.Expiration
        };
    }

    private async Task<PowerBIClient> CreateClientAsync()
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext(PowerBiScopes), CancellationToken.None);
        _logger.LogInformation("Acquired Power BI token using managed identity. Expires at {Expiration}", token.ExpiresOn);

        var credentials = new TokenCredentials(token.Token, "Bearer");
        var baseUri = string.IsNullOrEmpty(_overrideApiUrl) ? PowerBiApiRoot : new Uri(_overrideApiUrl);

        return new PowerBIClient(baseUri, credentials);
    }
}
