using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Auth;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IWebHostEnvironment _env;

    public AuthController(ITokenService tokenService, IWebHostEnvironment env)
    {
        _tokenService = tokenService;
        _env = env;
    }

    [HttpPost("token")]
    public IActionResult GetToken()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        return Ok(new { token = _tokenService.GenerateToken() });
    }
}

