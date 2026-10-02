using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace DCExpense.WebApi.Controllers;

public static class UserExtensions
{
    public const string DefaultDemoUserId = "718680ab-f7cb-41f2-b8c2-4fc9e2ad4594";

    public static string GetCurrentUserId(this ClaimsPrincipal? principal, HttpRequest? request = null)
    {
        var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? principal?.FindFirst("sub")?.Value 
                     ?? (request?.Headers != null && request.Headers.TryGetValue("X-User-Id", out var headerVal) ? headerVal.FirstOrDefault() : null);

        return !string.IsNullOrWhiteSpace(userId) ? userId : DefaultDemoUserId;
    }
}
