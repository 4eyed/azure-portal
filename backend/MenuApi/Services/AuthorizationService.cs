using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;

namespace MenuApi.Services;

/// <summary>
/// Implementation of authorization service using OpenFGA
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly OpenFgaClient _fgaClient;

    public AuthorizationService(OpenFgaClient fgaClient)
    {
        _fgaClient = fgaClient ?? throw new ArgumentNullException(nameof(fgaClient));
    }

    public async Task<bool> CanViewMenuItem(string userId, string menuItemName)
    {
        var checkRequest = new ClientCheckRequest
        {
            User = $"user:{userId}",
            Relation = "viewer",
            Object = $"menu_item:{menuItemName.ToLower().Replace(" ", "_")}"
        };

        var response = await _fgaClient.Check(checkRequest);
        return response.Allowed == true;
    }

    public async Task<Dictionary<string, bool>> CanViewMenuItems(string userId, IEnumerable<string> menuItemNames)
    {
        var results = new Dictionary<string, bool>();

        // Batch all authorization checks in parallel to reduce latency
        var checkTasks = menuItemNames.Select(async menuItemName =>
        {
            var allowed = await CanViewMenuItem(userId, menuItemName);
            return (menuItemName, allowed);
        });

        var checkResults = await Task.WhenAll(checkTasks);

        foreach (var (menuItemName, allowed) in checkResults)
        {
            results[menuItemName] = allowed;
        }

        return results;
    }

    public async Task<bool> IsAdmin(string userId)
    {
        var checkRequest = new ClientCheckRequest
        {
            User = $"user:{userId}",
            Relation = "assignee",
            Object = "role:admin"
        };

        var response = await _fgaClient.Check(checkRequest);
        return response.Allowed == true;
    }
}
