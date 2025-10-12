namespace MenuApi.Models.DTOs;

/// <summary>
/// Request DTO for generating embed token
/// </summary>
public class EmbedTokenRequest
{
    public required string WorkspaceId { get; set; }
    public required string ReportId { get; set; }
}

/// <summary>
/// Response DTO for embed token
/// </summary>
public class EmbedTokenResponse
{
    public required string Token { get; set; }
    public DateTime? Expiration { get; set; }
}
