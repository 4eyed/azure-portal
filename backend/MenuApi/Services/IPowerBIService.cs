using MenuApi.Models.DTOs;

namespace MenuApi.Services;

/// <summary>
/// Service for Power BI integration
/// </summary>
public interface IPowerBIService
{
    /// <summary>
    /// Gets all Power BI workspaces using the user's access token
    /// </summary>
    Task<List<PowerBIWorkspaceResponse>> GetWorkspaces(string userAccessToken);

    /// <summary>
    /// Gets all reports in a workspace using the user's access token
    /// </summary>
    Task<List<PowerBIReportResponse>> GetReports(string workspaceId, string userAccessToken);

    /// <summary>
    /// Generates an embed token for a report using the user's access token
    /// </summary>
    Task<EmbedTokenResponse> GenerateEmbedToken(string workspaceId, string reportId, string userAccessToken);
}
