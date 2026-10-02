using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace DCExpense.WebApi.Controllers;

public record LoginRequestDto(string Email, string Password);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequestDto request)
    {
        var email = string.IsNullOrWhiteSpace(request?.Email) ? "admin@dcmanager.local" : request.Email.Trim();
        var userId = "718680ab-f7cb-41f2-b8c2-4fc9e2ad4594";
        var fullName = "Triệu Quang Quân";

        var secret = _configuration["Jwt:Secret"] ?? "DCManager-Super-Secret-Key-That-Should-Be-At-Least-32-Characters-Long-2024!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("email", email),
            new Claim("name", fullName),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "DCManager",
            audience: _configuration["Jwt:Audience"] ?? "DCManagerClients",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            success = true,
            data = new
            {
                accessToken = tokenString,
                user = new
                {
                    id = userId,
                    email = email,
                    userName = email,
                    firstName = "Triệu Quang",
                    lastName = "Quân",
                    fullName = fullName,
                    avatarUrl = "https://amismisa.misacdn.net/APIS/PlatformAPI/api/Avatar/56c33c47-78ec-4b3a-92ba-9fdb7830a6d4/misa.jpg?avatarID=0f545a2e-22fe-42f2-9235-eebb8a4ff5e6",
                    roles = new[] { "SuperAdmin" }
                }
            }
        });
    }
}
