using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

[Route("api/[controller]")]
public class AuthController(ClinicDbContext db, TokenService tokens, IClinicClock clock, IConfiguration cfg) : ApiBase
{
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(15);

    /// <summary>Sign-up (algorithm 1.1). New accounts wait for an administrator's approval. Admin accounts cannot be self-registered.</summary>
    [HttpPost("register"), AllowAnonymous]
    public async Task<ActionResult> Register(RegisterRequest req)
    {
        if (!Enum.TryParse<UserRole>(req.Role, true, out var role) || role == UserRole.Admin)
            return Fail("Choose Staff or Doctor. Administrator accounts are created by an existing administrator.");

        var pwError = PasswordPolicy.Validate(req.Password);
        if (pwError is not null) return Fail(pwError);

        var username = req.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username))
            return Fail("That username is already taken. Choose another one.", 409);

        if (role == UserRole.Doctor && string.IsNullOrWhiteSpace(req.Specialty))
            return Fail("Enter your specialty.");

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            FullName = req.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Role = role,
            Status = AccountStatus.Pending,
            CreatedAt = clock.Now,
        };
        if (role == UserRole.Doctor)
        {
            user.Doctor = new Doctor
            {
                Specialty = req.Specialty!.Trim(),
                ConsultationFee = cfg.GetValue("Clinic:DefaultConsultationFee", 500m),
            };
        }
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return StatusCode(201, new { message = "Account created. An administrator must approve it before you can sign in." });
    }

    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var username = req.Username.Trim();
        var user = await db.Users.Include(u => u.Doctor).FirstOrDefaultAsync(u => u.Username == username);
        const string bad = "Incorrect username or password.";
        if (user is null) return Fail(bad, 401);

        if (user.LockoutEndUtc is { } until && until > DateTime.UtcNow)
        {
            var mins = Math.Max(1, (int)Math.Ceiling((until - DateTime.UtcNow).TotalMinutes));
            return Fail($"Too many failed attempts. Try again in {mins} minute{(mins == 1 ? "" : "s")}.", 401);
        }

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutTime);
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync();
            return Fail(bad, 401);
        }

        if (user.Status == AccountStatus.Pending) return Fail("Your account is waiting for administrator approval.", 403);
        if (user.Status == AccountStatus.Disabled) return Fail("This account has been disabled. Contact an administrator.", 403);

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAt = clock.Now;
        await db.SaveChangesAsync();

        var (token, expires) = tokens.Create(user, user.Doctor?.Id);
        return new LoginResponse(token, expires, new SessionUser(user.Id, user.Username, user.FullName, user.Role, user.Doctor?.Id));
    }

    [HttpGet("me"), Authorize]
    public async Task<ActionResult<SessionUser>> Me()
    {
        var u = await db.Users.Include(x => x.Doctor).FirstOrDefaultAsync(x => x.Id == CurrentUserId);
        return u is null ? Unauthorized() : new SessionUser(u.Id, u.Username, u.FullName, u.Role, u.Doctor?.Id);
    }

    [HttpPost("change-password"), Authorize]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var user = await db.Users.FindAsync(CurrentUserId);
        if (user is null) return Unauthorized();
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash)) return Fail("Current password is incorrect.");
        var pwError = PasswordPolicy.Validate(req.NewPassword);
        if (pwError is not null) return Fail(pwError);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { message = "Password changed." });
    }
}
