using MenuApi.Models.DTOs;

namespace MenuApi.Services;

/// <summary>
/// Service for Power BI integration
/// </summary>
public interface IPowerBIService
{
    /// <summary>
    /// Gets all Power BI workspaces using the application's credential
    /// </summary>
    Task<List<PowerBIWorkspaceResponse>> GetWorkspacesAsync();

    /// <summary>
    /// Gets all reports in a workspace using the application's credential
    /// </summary>
    Task<List<PowerBIReportResponse>> GetReportsAsync(string workspaceId);

    /// <summary>
    /// Generates an embed token for a report using the application's credential
    /// </summary>
    Task<EmbedTokenResponse> GenerateEmbedTokenAsync(string workspaceId, string reportId);
}
