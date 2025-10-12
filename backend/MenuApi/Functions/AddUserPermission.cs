using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;

namespace MenuApi.Functions;

public class AddUserPermission
{
    private readonly ILogger<AddUserPermission> _logger;
    private readonly OpenFgaClient _fgaClient;

    public AddUserPermission(ILogger<AddUserPermission> logger, OpenFgaClient fgaClient)
    {
        _logger = logger;
        _fgaClient = fgaClient;
    }

    [Function("AddUserPermission")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/add-user-permission")] HttpRequestData req)
    {
        _logger.LogInformation("Adding user permission to OpenFGA");

        try
        {
            // Parse request body
            var body = await req.ReadFromJsonAsync<AddUserPermissionRequest>();

            if (body == null || string.IsNullOrEmpty(body.UserId) || string.IsNullOrEmpty(body.Role))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "UserId and Role are required" });
                return badRequest;
            }

            // Add tuple to OpenFGA
            var tuple = new ClientTupleKey
            {
                User = $"user:{body.UserId}",
                Relation = "assignee",
                Object = $"role:{body.Role}"
            };

            await _fgaClient.Write(new ClientWriteRequest
            {
                Writes = new List<ClientTupleKey> { tuple }
            });

            _logger.LogInformation($"Successfully added permission: user:{body.UserId} -> role:{body.Role}");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = $"User {body.UserId} assigned to role {body.Role}",
                tuple = new
                {
                    user = tuple.User,
                    relation = tuple.Relation,
                    @object = tuple.Object
                }
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user permission");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}

public class AddUserPermissionRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
