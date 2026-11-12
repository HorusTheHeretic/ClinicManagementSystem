using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

[Route("api/[controller]"), Authorize]
public class AppointmentsController(ClinicDbContext db, IClinicClock clock, AvailabilityService availability) : ApiBase
{
    private Task<AppointmentDto> LoadDto(int id) => db.Appointments.Where(a => a.Id == id).ToAppointmentDto().FirstAsync();

    [HttpGet]
    public async Task<List<AppointmentDto>> List([FromQuery] DateTime? date, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int? doctorId, [FromQuery] AppointmentStatus? status, [FromQuery] int? patientId)
    {
        var q = db.Appointments.AsQueryable();
        if (CurrentRole == UserRole.Doctor) doctorId = CurrentDoctorId;   // doctors only ever see their own list
        if (doctorId.HasValue) q = q.Where(a => a.DoctorId == doctorId.Value);
        if (patientId.HasValue) q = q.Where(a => a.PatientId == patientId.Value);
        if (status.HasValue) q = q.Where(a => a.Status == status.Value);
        if (date.HasValue)
        {
            var d = date.Value.Date;
            q = q.Where(a => a.StartsAt >= d && a.StartsAt < d.AddDays(1));
        }
        else
        {
            if (from.HasValue) { var f = from.Value.Date; q = q.Where(a => a.StartsAt >= f); }
            if (to.HasValue) { var t = to.Value.Date.AddDays(1); q = q.Where(a => a.StartsAt < t); }
        }
        return await q.OrderBy(a => a.StartsAt).Take(500).ToAppointmentDto().ToListAsync();
    }

    [HttpGet("availability")]
    public async Task<ActionResult<List<SlotDto>>> Availability([FromQuery] int doctorId, [FromQuery] DateTime date, [FromQuery] int? ignoreAppointmentId)
    {
        var doctor = await db.Doctors.FindAsync(doctorId);
        if (doctor is null) return NotFound(new { message = "Doctor not found." });
        return await availability.GetSlotsAsync(doctor, date, ignoreAppointmentId);
    }

    /// <summary>Schedule an appointment (algorithm 1.4): check the doctor's availability, then create it.</summary>
    [HttpPost, Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<AppointmentDto>> Create(CreateAppointmentRequest req)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == req.PatientId && !p.IsDeleted)) return Fail("Patient not found.", 404);
        var doctor = await db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == req.DoctorId && d.User.Status == AccountStatus.Active);
        if (doctor is null) return Fail("Doctor not found.", 404);

        var startsAt = DateTime.SpecifyKind(req.StartsAt, DateTimeKind.Unspecified);
        if (!await availability.IsAvailableAsync(doctor, startsAt))
            return Fail("That time is not available. Pick another slot.", 409);

        var now = clock.Now;
        var appt = new Appointment
        {
            PatientId = req.PatientId,
            DoctorId = doctor.Id,
            StartsAt = startsAt,
            DurationMinutes = doctor.SlotMinutes,
            SlotKey = Projections.SlotKeyFor(doctor.Id, startsAt),
            Reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim(),
            Status = req.Confirm ? AppointmentStatus.Confirmed : AppointmentStatus.Scheduled,
            CreatedByUserId = CurrentUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        try
        {
            await db.AddWithNumberAsync(appt, (x, n) => x.AppointmentNo = n, x => $"APT-{x.Id:D6}");
        }
        catch (DbUpdateException)
        {
            return Fail("Someone just booked that time. Pick another slot.", 409);
        }
        return StatusCode(201, await LoadDto(appt.Id));
    }

    [HttpPut("{id:int}/reschedule"), Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<AppointmentDto>> Reschedule(int id, RescheduleRequest req)
    {
        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appt is null) return NotFound(new { message = "Appointment not found." });
        if (appt.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
            return Fail("Only scheduled or confirmed appointments can be rescheduled.", 409);

        var targetDoctorId = req.DoctorId ?? appt.DoctorId;   // resolved here: EF cannot translate a nullable ?? inside the query
        var doctor = await db.Doctors.Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == targetDoctorId && d.User.Status == AccountStatus.Active);
        if (doctor is null) return Fail("Doctor not found.", 404);

        var startsAt = DateTime.SpecifyKind(req.StartsAt, DateTimeKind.Unspecified);
        if (!await availability.IsAvailableAsync(doctor, startsAt, appt.Id))
            return Fail("That time is not available. Pick another slot.", 409);

        appt.DoctorId = doctor.Id;
        appt.StartsAt = startsAt;
        appt.DurationMinutes = doctor.SlotMinutes;
        appt.SlotKey = Projections.SlotKeyFor(doctor.Id, startsAt);
        appt.Status = AppointmentStatus.Scheduled;   // the patient must be told about the change and confirm again
        appt.UpdatedAt = clock.Now;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Fail("Someone just booked that time. Pick another slot.", 409); }
        return await LoadDto(id);
    }

    [HttpPost("{id:int}/confirm"), Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<AppointmentDto>> Confirm(int id)
    {
        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appt is null) return NotFound(new { message = "Appointment not found." });
        if (appt.Status != AppointmentStatus.Scheduled) return Fail("Only scheduled appointments can be confirmed.", 409);
        appt.Status = AppointmentStatus.Confirmed;
        appt.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return await LoadDto(id);
    }

    [HttpPost("{id:int}/cancel"), Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<AppointmentDto>> Cancel(int id, CancelRequest req)
    {
        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appt is null) return NotFound(new { message = "Appointment not found." });
        if (appt.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.Waiting))
            return Fail("This appointment can no longer be cancelled.", 409);
        appt.Status = AppointmentStatus.Cancelled;
        appt.CancelReason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
        appt.SlotKey = null;   // frees the slot for someone else
        appt.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return await LoadDto(id);
    }

    /// <summary>The patient did not show up. Also removes them from the waiting line.</summary>
    [HttpPost("{id:int}/no-show")]
    public async Task<ActionResult<AppointmentDto>> NoShow(int id)
    {
        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appt is null) return NotFound(new { message = "Appointment not found." });
        if (CurrentRole == UserRole.Doctor && appt.DoctorId != CurrentDoctorId) return Forbid();
        if (appt.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.Waiting or AppointmentStatus.InConsultation))
            return Fail("This appointment is already closed.", 409);
        appt.Status = AppointmentStatus.NoShow;
        appt.SlotKey = null;
        appt.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return await LoadDto(id);
    }
}
