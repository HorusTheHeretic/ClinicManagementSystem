using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

[Route("api/[controller]"), Authorize]
public class QueueController(ClinicDbContext db, IClinicClock clock, QueueService queue) : ApiBase
{
    private Task<AppointmentDto> LoadDto(int id) => db.Appointments.Where(a => a.Id == id).ToAppointmentDto().FirstAsync();

    /// <summary>Today's line: patients being seen first, then waiting patients (urgent first, then by queue number).</summary>
    [HttpGet]
    public async Task<List<AppointmentDto>> Today([FromQuery] int? doctorId)
    {
        var today = clock.Today;
        if (CurrentRole == UserRole.Doctor) doctorId = CurrentDoctorId;
        var q = db.Appointments.Where(a => a.QueueDate == today
            && (a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.InConsultation));
        if (doctorId.HasValue) q = q.Where(a => a.DoctorId == doctorId.Value);

        var list = await q.ToAppointmentDto().ToListAsync();
        return list
            .OrderBy(a => a.DoctorName)
            .ThenBy(a => a.Status == AppointmentStatus.InConsultation ? 0 : 1)
            .ThenBy(a => a.Priority == QueuePriority.Urgent ? 0 : 1)
            .ThenBy(a => a.QueueNumber)
            .ToList();
    }

    /// <summary>Public waiting-room display. Exposes queue numbers only, never patient names.</summary>
    [HttpGet("board"), AllowAnonymous]
    public async Task<ActionResult> Board([FromServices] IConfiguration cfg)
    {
        var today = clock.Today;
        var doctors = await db.Doctors.Where(d => d.User.Status == AccountStatus.Active)
            .OrderBy(d => d.User.FullName)
            .Select(d => new { d.Id, Name = d.User.FullName, d.Specialty }).ToListAsync();
        var entries = await db.Appointments
            .Where(a => a.QueueDate == today && (a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.InConsultation))
            .Select(a => new { a.DoctorId, a.QueueNumber, a.Status, a.Priority }).ToListAsync();

        var board = doctors.Select(d =>
        {
            var mine = entries.Where(e => e.DoctorId == d.Id).ToList();
            var waiting = mine.Where(e => e.Status == AppointmentStatus.Waiting)
                .OrderBy(e => e.Priority == QueuePriority.Urgent ? 0 : 1).ThenBy(e => e.QueueNumber)
                .Select(e => e.QueueNumber ?? 0).ToList();
            var serving = mine.FirstOrDefault(e => e.Status == AppointmentStatus.InConsultation)?.QueueNumber;
            return new BoardDoctorDto(d.Id, d.Name, d.Specialty, serving, waiting.Take(8).ToList(), waiting.Count);
        }).ToList();

        return Ok(new { clinicName = cfg["Clinic:Name"] ?? "Clinic", updatedAt = clock.Now, doctors = board });
    }

    /// <summary>Patient arrives: confirmed appointment goes into today's queue (algorithm 1.5).</summary>
    [HttpPost("check-in/{id:int}"), Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<AppointmentDto>> CheckIn(int id, CheckInRequest? req)
    {
        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appt is null) return NotFound(new { message = "Appointment not found." });
        if (appt.Status == AppointmentStatus.Scheduled) return Fail("Confirm the appointment before checking the patient in.", 409);
        if (appt.Status != AppointmentStatus.Confirmed) return Fail("Only confirmed appointments can be checked in.", 409);
        if (appt.StartsAt.Date != clock.Today) return Fail("Patients can only be checked in on the day of their appointment.", 409);

        await queue.CheckInAsync(appt, req?.Priority ?? QueuePriority.Normal);
        return await LoadDto(id);
    }

    /// <summary>A patient without an appointment: create one for right now and put them straight in the line.</summary>
    [HttpPost("walk-in"), Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<AppointmentDto>> WalkIn(WalkInRequest req)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == req.PatientId && !p.IsDeleted)) return Fail("Patient not found.", 404);
        var doctor = await db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == req.DoctorId && d.User.Status == AccountStatus.Active);
        if (doctor is null) return Fail("Doctor not found.", 404);

        var today = clock.Today;
        if (await db.Appointments.AnyAsync(a => a.PatientId == req.PatientId && a.QueueDate == today
                && (a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.InConsultation)))
            return Fail("This patient is already in today's queue.", 409);

        var now = clock.Now;
        var appt = new Appointment
        {
            PatientId = req.PatientId,
            DoctorId = doctor.Id,
            StartsAt = now,
            DurationMinutes = doctor.SlotMinutes,
            Reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim(),
            Status = AppointmentStatus.Confirmed,
            IsWalkIn = true,
            CreatedByUserId = CurrentUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await db.AddWithNumberAsync(appt, (x, n) => x.AppointmentNo = n, x => $"APT-{x.Id:D6}");
        await queue.CheckInAsync(appt, req.Priority);
        return StatusCode(201, await LoadDto(appt.Id));
    }

    /// <summary>The doctor calls the next waiting patient: status becomes "In Consultation" and the patient leaves the waiting line.</summary>
    [HttpPost("call-next"), Authorize(Roles = "Doctor")]
    public async Task<ActionResult<AppointmentDto>> CallNext()
    {
        var doctorId = CurrentDoctorId!.Value;
        var today = clock.Today;

        var current = await db.Appointments.FirstOrDefaultAsync(a => a.DoctorId == doctorId && a.QueueDate == today
            && a.Status == AppointmentStatus.InConsultation);
        if (current is not null)
            return Conflict(new { message = "Finish the current consultation before calling the next patient.", appointmentId = current.Id });

        var next = await queue.NextWaitingAsync(doctorId);
        if (next is null) return Fail("No patients are waiting.", 404);

        next.Status = AppointmentStatus.InConsultation;
        next.CalledAt = clock.Now;
        next.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return await LoadDto(next.Id);
    }
}
