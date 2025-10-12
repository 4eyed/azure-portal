namespace MenuApi.Models.DTOs;

/// <summary>
/// Standard error response DTO
/// </summary>
public class ErrorResponse
{
    public required string Error { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string[]>? ValidationErrors { get; set; }
}
