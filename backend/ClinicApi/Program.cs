using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using ClinicApi.Data;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- Database (EF Core + Pomelo MySQL) ----------
var connectionString = cfg.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");
// "8.0.36" for MySQL, or "10.11.0-mariadb" for MariaDB.
var versionText = cfg["Database:ServerVersion"] ?? "8.0.36";
var versionNumber = Version.Parse(versionText.Split('-')[0]);
ServerVersion serverVersion = versionText.Contains("mariadb", StringComparison.OrdinalIgnoreCase)
    ? new MariaDbServerVersion(versionNumber)
    : new MySqlServerVersion(versionNumber);
builder.Services.AddDbContext<ClinicDbContext>(o => o.UseMySql(connectionString, serverVersion));

// ---------- JWT authentication ----------
builder.Services.Configure<JwtOptions>(cfg.GetSection("Jwt"));
var jwt = cfg.GetSection("Jwt").Get<JwtOptions>() ?? throw new InvalidOperationException("Jwt section is missing.");
if (jwt.Key.Length < 32 || jwt.Key.StartsWith("REPLACE-WITH", StringComparison.Ordinal) && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Set Jwt:Key to a random secret of at least 32 characters (user-secrets or the Jwt__Key environment variable).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;   // keep the short claim names we issue: sub, role, name ...
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        RoleClaimType = "role",
        NameClaimType = "name",
    };
    // A disabled account stops working immediately instead of when its token expires.
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<ClinicDbContext>();
            if (!int.TryParse(ctx.Principal?.FindFirstValue("sub"), out var userId)) { ctx.Fail("Invalid token."); return; }
            var active = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Status == AccountStatus.Active);
            if (!active) ctx.Fail("Account is not active.");
        },
    };
});
builder.Services.AddAuthorization();

// ---------- CORS for the React app ----------
var origins = cfg.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// ---------- App services ----------
builder.Services.AddSingleton<IClinicClock, ClinicClock>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<QueueService>();

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(new { message = "Something went wrong on the server. Try again, and tell an administrator if it keeps happening." });
}));

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Create the schema on first run (skipped when database/schema.sql was already applied), then seed.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
    if (cfg.GetValue("Database:AutoCreate", true)) await db.Database.EnsureCreatedAsync();
}
await DbSeeder.SeedAsync(app.Services);

app.Run();
