using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClinicApi.Data;
using ClinicApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClinicApi.Services;

/// <summary>Clinic local time. The server may run in UTC (Docker, cloud), but queue and scheduling logic must follow the clinic's wall clock.</summary>
public interface IClinicClock
{
    DateTime Now { get; }
    DateTime Today { get; }
}

public class ClinicClock : IClinicClock
{
    private readonly TimeZoneInfo _tz;

    public ClinicClock(IConfiguration cfg)
    {
        var id = cfg["Clinic:TimeZoneId"];
        try { _tz = string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { _tz = TimeZoneInfo.Local; }
        catch (InvalidTimeZoneException) { _tz = TimeZoneInfo.Local; }
    }

    public DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz), DateTimeKind.Unspecified);
    public DateTime Today => Now.Date;
}

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "ClinicApi";
    public string Audience { get; set; } = "ClinicWeb";
    public int ExpiryMinutes { get; set; } = 480;
}

public class TokenService(IOptions<JwtOptions> options)
{
    public (string Token, DateTime ExpiresUtc) Create(User user, int? doctorId)
    {
        var o = options.Value;
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("unique_name", user.Username),
            new("name", user.FullName),
            new("role", user.Role.ToString()),
        };
        if (doctorId.HasValue) claims.Add(new Claim("doctorId", doctorId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key));
        var expires = DateTime.UtcNow.AddMinutes(o.ExpiryMinutes);
        var jwt = new JwtSecurityToken(o.Issuer, o.Audience, claims, expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}

public static class PasswordPolicy
{
    /// <summary>Returns an error message, or null when the password is acceptable.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return "Password must be at least 8 characters.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "Password must include at least one letter and one number.";
        return null;
    }
}

public static class DbExtensions
{
    /// <summary>
    /// Inserts a row, then rewrites its business number (PT-2026-00012, INV-000045 ...) from the generated Id.
    /// Runs inside the caller's transaction when one is already open.
    /// </summary>
    public static async Task AddWithNumberAsync<T>(this ClinicDbContext db, T entity,
        Action<T, string> setNumber, Func<T, string> makeNumber) where T : class
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var tx = ownsTransaction ? await db.Database.BeginTransactionAsync() : null;

        setNumber(entity, "TMP-" + Guid.NewGuid().ToString("N")[..12]);
        db.Add(entity);
        await db.SaveChangesAsync();
        setNumber(entity, makeNumber(entity));
        await db.SaveChangesAsync();

        if (tx is not null) await tx.CommitAsync();
    }
}
