using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

[Route("api/[controller]"), Authorize]
public class DoctorsController(ClinicDbContext db) : ApiBase
{
    private static DoctorDto ToDto(Doctor d) => new(d.Id, d.UserId, d.User.FullName, d.Specialty, d.WorkDays,
        d.StartTime.ToString(@"hh\:mm"), d.EndTime.ToString(@"hh\:mm"), d.SlotMinutes, d.ConsultationFee, d.User.Status);

    /// <summary>Doctors that can take appointments (active accounts only).</summary>
    [HttpGet]
    public async Task<List<DoctorDto>> List()
    {
        var docs = await db.Doctors.Include(d => d.User)
            .Where(d => d.User.Status == AccountStatus.Active)
            .OrderBy(d => d.User.FullName).ToListAsync();
        return docs.Select(ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DoctorDto>> Get(int id)
    {
        var d = await db.Doctors.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        return d is null ? NotFound() : ToDto(d);
    }

    /// <summary>Working days, hours, slot length and fee. Admins can edit any doctor, a doctor only their own.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<DoctorDto>> Update(int id, UpdateDoctorRequest req)
    {
        var isAdmin = CurrentRole == UserRole.Admin;
        var isOwner = CurrentRole == UserRole.Doctor && CurrentDoctorId == id;
        if (!isAdmin && !isOwner) return Forbid();

        var d = await db.Doctors.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();

        if (!TimeSpan.TryParse(req.StartTime, out var start) || !TimeSpan.TryParse(req.EndTime, out var end) || end <= start)
            return Fail("Enter valid working hours. The end time must be after the start time.");
        var days = AvailabilityService.ParseDays(req.WorkDays);
        if (days.Count == 0) return Fail("Select at least one working day.");

        d.Specialty = req.Specialty.Trim();
        d.WorkDays = string.Join(',', days.Select(x => (int)x).OrderBy(x => x));
        d.StartTime = start;
        d.EndTime = end;
        d.SlotMinutes = req.SlotMinutes;
        // Only an administrator may change the consultation fee.
        if (isAdmin) d.ConsultationFee = req.ConsultationFee;
        await db.SaveChangesAsync();
        return ToDto(d);
    }
}
