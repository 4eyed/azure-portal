using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using MenuApi.Services;
using MenuApi.Extensions;
using MenuApi.Infrastructure;

namespace MenuApi.Functions;

public class AddUserPermission
{
    private readonly ILogger<AddUserPermission> _logger;
    private readonly OpenFgaClient _fgaClient;
    private readonly IAuthorizationService _authService;
    private readonly IClaimsPrincipalParser _claimsParser;

    public AddUserPermission(
        ILogger<AddUserPermission> logger,
        OpenFgaClient fgaClient,
        IAuthorizationService authService,
        IClaimsPrincipalParser claimsParser)
    {
        _logger = logger;
        _fgaClient = fgaClient;
        _authService = authService;
        _claimsParser = claimsParser;
    }

    [Function("AddUserPermission")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/assign-user-permission")] HttpRequest req)
    {
        _logger.LogInformation("Adding user permission to OpenFGA");

        try
        {
            // Extract authenticated user ID
            var adminUserId = req.GetAuthenticatedUserId(_claimsParser);
            if (string.IsNullOrEmpty(adminUserId))
            {
                return new CorsObjectResult(new { error = "User is not authenticated" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            // TEMPORARY: Admin check disabled to allow initial admin assignment
            // TODO: Re-enable after first admin is assigned
            // Check if user is admin
            // if (!req.IsAdmin(_claimsParser) && !await _authService.IsAdmin(adminUserId))
            // {
            //     return new CorsObjectResult(new { error = "Only admins can assign user permissions" })
            //     {
            //         StatusCode = StatusCodes.Status403Forbidden
            //     };
            // }
            _logger.LogWarning("Admin check temporarily disabled - any authenticated user can assign permissions!");

            // Parse request body
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = System.Text.Json.JsonSerializer.Deserialize<AddUserPermissionRequest>(body, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Relation) || string.IsNullOrEmpty(request.Object))
            {
                return new CorsObjectResult(new { error = "UserId, Relation, and Object are required" })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            // Add tuple to OpenFGA (userId should be Entra OID)
            var tuple = new ClientTupleKey
            {
                User = $"user:{request.UserId}",
                Relation = request.Relation,
                Object = request.Object
            };

            await _fgaClient.Write(new ClientWriteRequest
            {
                Writes = new List<ClientTupleKey> { tuple }
            });

            _logger.LogInformation($"Successfully added permission: {tuple.User} -> {tuple.Relation} -> {tuple.Object}");

            return new CorsObjectResult(new
            {
                success = true,
                message = $"User {request.UserId} assigned {request.Relation} on {request.Object}",
                tuple = new
                {
                    user = tuple.User,
                    relation = tuple.Relation,
                    @object = tuple.Object
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user permission");
            return new CorsObjectResult(new { error = ex.Message })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}

public class AddUserPermissionRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
}
