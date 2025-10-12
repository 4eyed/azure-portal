using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using MenuApi.Models.DTOs;

namespace MenuApi.Services;

/// <summary>
/// Implementation of Power BI service using delegated user permissions
/// </summary>
public class PowerBIService : IPowerBIService
{
    private PowerBIClient GetClient(string accessToken)
    {
        var credentials = new TokenCredentials(accessToken, "Bearer");
        return new PowerBIClient(credentials);
    }

    public async Task<List<PowerBIWorkspaceResponse>> GetWorkspaces(string userAccessToken)
    {
        try
        {
            using var client = GetClient(userAccessToken);
            var groups = await client.Groups.GetGroupsAsync();

            return groups.Value.Select(g => new PowerBIWorkspaceResponse
            {
                Id = g.Id.ToString(),
                Name = g.Name
            }).ToList();
        }
        catch (Microsoft.Rest.HttpOperationException ex)
        {
            var errorMessage = $"Power BI API returned {ex.Response?.StatusCode}: {ex.Response?.Content}";
            throw new InvalidOperationException(
                $"Failed to fetch Power BI workspaces. {errorMessage}. " +
                "Please ensure: (1) User has Power BI Pro/Premium license, " +
                "(2) User has access to at least one workspace, " +
                "(3) Token has correct Power BI scopes.", ex);
        }
    }

    public async Task<List<PowerBIReportResponse>> GetReports(string workspaceId, string userAccessToken)
    {
        if (string.IsNullOrEmpty(workspaceId))
        {
            throw new ArgumentException("Workspace ID is required", nameof(workspaceId));
        }

        using var client = GetClient(userAccessToken);
        var reports = await client.Reports.GetReportsInGroupAsync(Guid.Parse(workspaceId));

        return reports.Value.Select(r => new PowerBIReportResponse
        {
            Id = r.Id.ToString(),
            Name = r.Name,
            EmbedUrl = r.EmbedUrl
        }).ToList();
    }

    public async Task<EmbedTokenResponse> GenerateEmbedToken(string workspaceId, string reportId, string userAccessToken)
    {
        if (string.IsNullOrEmpty(workspaceId) || string.IsNullOrEmpty(reportId))
        {
            throw new ArgumentException("Workspace ID and Report ID are required");
        }

        // Use the user's access token for embed token generation
        using var client = GetClient(userAccessToken);

        var generateTokenRequest = new GenerateTokenRequest(
            accessLevel: "View",
            datasetId: null
        );

        var embedToken = await client.Reports.GenerateTokenInGroupAsync(
            Guid.Parse(workspaceId),
            Guid.Parse(reportId),
            generateTokenRequest
        );

        return new EmbedTokenResponse
        {
            Token = embedToken.Token,
            Expiration = embedToken.Expiration
        };
    }
}
