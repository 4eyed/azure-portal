namespace MenuApi.Models.DTOs;

/// <summary>
/// Response DTO for Power BI reports
/// </summary>
public class PowerBIReportResponse
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string EmbedUrl { get; set; }
}
