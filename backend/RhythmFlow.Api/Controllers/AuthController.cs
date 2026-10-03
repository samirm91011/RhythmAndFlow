using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Controllers;

public abstract class ApiController : ControllerBase
{
    protected Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    protected bool IsAdmin => User.IsInRole(Roles.Admin);
}

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens) : ApiController
{
    private static readonly PasswordHasher<User> Hasher = new();

    [HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var username = req.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Email == email)) return Conflict(new { error = "That email is already registered." });
        if (await db.Users.AnyAsync(u => u.Username == username)) return Conflict(new { error = "That username is taken." });

        // Public registration can only create customers; admins are created by seeding or by another admin.
        var user = new User { FullName = req.FullName.Trim(), Username = username, Email = email, Role = Roles.Customer };
        user.PasswordHash = Hasher.HashPassword(user, req.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Ok(new AuthResponse(tokens.CreateToken(user), ToDto(user)));
    }

    [HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var id = req.Identifier.Trim();
        var lower = id.ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == lower || u.Username == id);
        // Same message for unknown user and wrong password so accounts cannot be enumerated.
        if (user is null || user.AccountStatus != "ACTIVE" ||
            Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Incorrect username/email or password." });
        return Ok(new AuthResponse(tokens.CreateToken(user), ToDto(user)));
    }

    /// <summary>Emails a 6-digit code. Always answers the same way so nobody can test which emails have accounts.</summary>
    [HttpPost("forgot-password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req, [FromServices] PasswordResetService reset)
    {
        await reset.RequestAsync(req.Email);
        return Ok(new { message = "If that email has an account, we've sent a 6-digit code. It expires in 15 minutes." });
    }

    [HttpPost("reset-password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req, [FromServices] PasswordResetService reset)
    {
        var (ok, error) = await reset.ResetAsync(req.Email, req.Code, req.NewPassword);
        return ok ? Ok(new { message = "Your password has been changed. You can log in now." }) : BadRequest(new { error });
    }

    [Authorize, HttpPost("change-password"), EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest req)
    {
        var user = await db.Users.FindAsync(UserId);
        if (user is null) return Unauthorized();
        if (Hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { error = "Your current password isn't right." });
        user.PasswordHash = Hasher.HashPassword(user, req.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N"); // signs out other devices
        await db.SaveChangesAsync();
        return Ok(new AuthResponse(tokens.CreateToken(user), ToDto(user))); // this device gets a fresh token
    }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await db.Users.FindAsync(UserId);
        return user is null ? Unauthorized() : Ok(ToDto(user));
    }

    [Authorize, HttpPut("me")]
    public async Task<ActionResult<UserDto>> Update(UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(UserId);
        if (user is null) return Unauthorized();
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != user.Id))
            return Conflict(new { error = "That email is already registered." });
        user.FullName = req.FullName.Trim();
        user.Email = email;
        user.About = req.About?.Trim() ?? "";
        await db.SaveChangesAsync();
        return Ok(ToDto(user));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.FullName, u.Username, u.Email, u.Role, u.About);
}
