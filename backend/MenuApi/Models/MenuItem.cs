namespace MenuApi.Models;

public class MenuItem
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public required string Url { get; set; }
    public string? Description { get; set; }
}
