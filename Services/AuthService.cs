using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CloudCacheManager.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CloudCacheManager.Services;

public class AuthService
{
    private readonly IConfiguration _config;
    private readonly AppSettings _appSettings;

    public AuthService(IConfiguration config, IOptions<AppSettings> appSettings)
    {
        _config = config;
        _appSettings = appSettings.Value;
    }

    public LoginResponse? Authenticate(LoginRequest request)
    {
        // All users come from appsettings.json → AppSettings:Users
        var user = _appSettings.Users
            .FirstOrDefault(u => u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)
                              && u.Password == request.Password);

        if (user == null) return null;

        var expiryHours = _config.GetValue<int>("Jwt:ExpiryHours", 8);
        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);

        return new LoginResponse
        {
            Token = GenerateToken(user.Username, user.Role, expiresAt),
            Username = user.Username,
            Role = user.Role,
            ExpiresAt = expiresAt
        };
    }

    private string GenerateToken(string username, string role, DateTime expiresAt)
    {
        var key = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is missing in appsettings.json");

        var secKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(secKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
