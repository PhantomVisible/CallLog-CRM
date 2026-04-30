using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CallLogCRM.Api.Data;
using CallLogCRM.Api.DTOs;
using CallLogCRM.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CallLogCRM.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    // POST /api/auth/select-closer
    // Passwordless kiosk login — the closer just picks their name from the list.
    [HttpPost("select-closer")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SelectCloser([FromBody] SelectCloserRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await db.Users.SingleOrDefaultAsync(u => u.CloserName == dto.CloserName);
        if (user is null || user.Role != "Closer")
            return Unauthorized();

        return Ok(new AuthResponseDto { Token = BuildToken(user), CloserName = user.CloserName });
    }

    // POST /api/auth/admin-login
    // Password-protected login — only succeeds when Role == "Admin".
    [HttpPost("admin-login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AdminLogin([FromBody] AdminLoginRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await db.Users.SingleOrDefaultAsync(u => u.CloserName == dto.CloserName);
        if (user is null || user.Role != "Admin" || user.PasswordHash is null)
            return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized();

        return Ok(new AuthResponseDto { Token = BuildToken(user), CloserName = user.CloserName });
    }

    private string BuildToken(User user)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name,           user.CloserName),
            new(ClaimTypes.Role,           user.Role)
        ];

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             config["Jwt:Issuer"],
            audience:           config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
