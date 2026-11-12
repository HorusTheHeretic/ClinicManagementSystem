using System.Security.Claims;
using ClinicApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace ClinicApi.Services;

[ApiController]
public abstract class ApiBase : ControllerBase
{
    protected int CurrentUserId => int.Parse(User.FindFirstValue("sub")!);
    protected UserRole CurrentRole => Enum.Parse<UserRole>(User.FindFirstValue("role")!);
    protected int? CurrentDoctorId => int.TryParse(User.FindFirstValue("doctorId"), out var id) ? id : null;

    protected ObjectResult Fail(string message, int status = StatusCodes.Status400BadRequest) =>
        StatusCode(status, new { message });
}
