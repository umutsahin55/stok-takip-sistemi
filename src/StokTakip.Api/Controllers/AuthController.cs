using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StokTakip.Application;
using StokTakip.Application.Services;

namespace StokTakip.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AuthService auth, IConfiguration cfg, ICurrentUser me) : ControllerBase
{
    static CookieOptions Cookie(DateTime? exp = null) => new() { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", Expires = exp };

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResultDto>> Login(LoginDto dto, CancellationToken ct)
    {
        var r = await auth.LoginAsync(dto, ct);
        Response.Cookies.Append("access_token", r.Token, Cookie(r.ExpiresAt));
        // Tarayıcı istemcisi yalnızca HttpOnly cookie kullanmalı; gövdede token yalnızca Swagger/Postman için (Development).
        return Ok(cfg.GetValue<bool>("Auth:ReturnTokenInBody") ? r : r with { Token = "" });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(ct);
        Response.Cookies.Delete("access_token", Cookie());
        return NoContent();
    }

    [HttpGet("me")]
    public IActionResult Me() => Ok(new { id = me.UserId, role = me.UserRole });
}
