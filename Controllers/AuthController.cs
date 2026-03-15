using CloudCacheManager.Models;
using CloudCacheManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace CloudCacheManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var result = _authService.Authenticate(request);
        if (result == null)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid username or password"
            });

        return Ok(new ApiResponse<LoginResponse>
        {
            Success = true,
            Message = "Login successful",
            Data = result
        });
    }
}
