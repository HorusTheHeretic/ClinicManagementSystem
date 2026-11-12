using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

[Route("api/[controller]"), Authorize(Roles = "Admin")]
public class UsersController(ClinicDbContext db, IClinicClock clock, IConfiguration cfg) : ApiBase
{
    private static UserDto ToDto(User u) => new(u.Id, u.Username, u.FullName, u.Email, u.Role, u.Status,
        u.CreatedAt, u.LastLoginAt, u.Doctor?.Id, u.Doctor?.Specialty);

    [HttpGet]
    public async Task<List<UserDto>> List([FromQuery] AccountStatus? status)
    {
        var q = db.Users.Include(u => u.Doctor).AsQueryable();
        if (status.HasValue) q = q.Where(u => u.Status == status.Value);
        var users = await q.OrderBy(u => u.Status).ThenBy(u => u.FullName).ToListAsync();
        return users.Select(ToDto).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest req)
    {
        var pwError = PasswordPolicy.Validate(req.Password);
        if (pwError is not null) return Fail(pwError);
        var username = req.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username)) return Fail("That username is already taken.", 409);
        if (req.Role == UserRole.Doctor && string.IsNullOrWhiteSpace(req.Specialty)) return Fail("Enter the doctor's specialty.");

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            FullName = req.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            Role = req.Role,
            Status = AccountStatus.Active,
            CreatedAt = clock.Now,
        };
        if (req.Role == UserRole.Doctor)
            user.Doctor = new Doctor { Specialty = req.Specialty!.Trim(), ConsultationFee = cfg.GetValue("Clinic:DefaultConsultationFee", 500m) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return StatusCode(201, ToDto(user));
    }

    /// <summary>Approve a pending sign-up, disable an account or enable it again.</summary>
    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<UserDto>> SetStatus(int id, SetStatusRequest req)
    {
        var user = await db.Users.Include(u => u.Doctor).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (user.Id == CurrentUserId && req.Status != AccountStatus.Active) return Fail("You cannot disable your own account.");
        if (req.Status == AccountStatus.Pending) return Fail("An account cannot be moved back to pending.");
        user.Status = req.Status;
        await db.SaveChangesAsync();
        return ToDto(user);
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<ActionResult> ResetPassword(int id, ResetPasswordRequest req)
    {
        var pwError = PasswordPolicy.Validate(req.NewPassword);
        if (pwError is not null) return Fail(pwError);
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        await db.SaveChangesAsync();
        return Ok(new { message = "Password reset." });
    }
}
