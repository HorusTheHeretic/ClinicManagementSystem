using System.Text.RegularExpressions;
using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

[Route("api/[controller]"), Authorize]
public class PatientsController(ClinicDbContext db, IClinicClock clock) : ApiBase
{
    private static readonly string[] Genders = ["Male", "Female", "Other"];
    private static readonly Regex PhoneRx = new(@"^[0-9+()\-\s]{7,30}$");

    private static readonly AppointmentStatus[] ActiveStatuses =
        [AppointmentStatus.Scheduled, AppointmentStatus.Confirmed, AppointmentStatus.Waiting, AppointmentStatus.InConsultation];

    /// <summary>Search by name, patient number or contact number (algorithm 1.3).</summary>
    [HttpGet]
    public async Task<PagedResult<PatientDto>> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = db.Patients.Where(p => !p.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p => p.FirstName.Contains(s) || p.LastName.Contains(s) || (p.FirstName + " " + p.LastName).Contains(s)
                             || p.PatientNo.Contains(s) || p.ContactNumber.Contains(s));
        }
        var total = await q.CountAsync();
        var rows = await q.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var today = clock.Today;
        return new PagedResult<PatientDto>(rows.Select(p => p.ToDto(today)).ToList(), total);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PatientDto>> Get(int id)
    {
        var p = await db.Patients.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return p is null ? NotFound(new { message = "Patient not found." }) : p.ToDto(clock.Today);
    }

    /// <summary>Medical history is clinical data: doctors and administrators only.</summary>
    [HttpGet("{id:int}/history"), Authorize(Roles = "Doctor,Admin")]
    public async Task<ActionResult<List<HistoryItemDto>>> History(int id)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == id && !p.IsDeleted)) return NotFound(new { message = "Patient not found." });
        var rows = await db.Consultations.Where(c => c.PatientId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new HistoryItemDto(c.Id, c.CreatedAt, c.Doctor.User.FullName, c.Symptoms, c.Diagnosis,
                c.Treatment, c.Prescription, c.Notes, c.FollowUpDate))
            .ToListAsync();
        return rows;
    }

    /// <summary>Register a patient (algorithm 1.2): validate, look for an existing record, then generate the Patient ID.</summary>
    [HttpPost, Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<PatientDto>> Create(PatientRequest req)
    {
        var error = Validate(req, out var gender);
        if (error is not null) return Fail(error);

        var first = req.FirstName.Trim();
        var last = req.LastName.Trim();
        var dob = req.DateOfBirth.Date;
        var existing = await db.Patients.FirstOrDefaultAsync(p => !p.IsDeleted && p.FirstName == first && p.LastName == last && p.DateOfBirth == dob);
        if (existing is not null)
            return Conflict(new { message = "This patient already has a record.", existing = existing.ToDto(clock.Today) });

        var now = clock.Now;
        var patient = new Patient { CreatedAt = now, UpdatedAt = now };
        Apply(patient, req, gender);
        await db.AddWithNumberAsync(patient, (x, n) => x.PatientNo = n, x => $"PT-{now.Year}-{x.Id:D5}");
        return StatusCode(201, patient.ToDto(clock.Today));
    }

    [HttpPut("{id:int}"), Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<PatientDto>> Update(int id, PatientRequest req)
    {
        var error = Validate(req, out var gender);
        if (error is not null) return Fail(error);
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (patient is null) return NotFound(new { message = "Patient not found." });
        Apply(patient, req, gender);
        patient.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return patient.ToDto(clock.Today);
    }

    /// <summary>Soft delete: the record is hidden but billing and consultation history stay intact.</summary>
    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (patient is null) return NotFound(new { message = "Patient not found." });
        if (await db.Appointments.AnyAsync(a => a.PatientId == id && ActiveStatuses.Contains(a.Status)))
            return Fail("This patient still has open appointments. Cancel or complete them first.", 409);
        patient.IsDeleted = true;
        patient.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private string? Validate(PatientRequest req, out string gender)
    {
        gender = Genders.FirstOrDefault(g => g.Equals(req.Gender?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "";
        if (gender == "") return "Select a gender.";
        if (req.DateOfBirth.Date > clock.Today) return "Date of birth cannot be in the future.";
        if (Projections.AgeOn(req.DateOfBirth, clock.Today) > 130) return "Check the date of birth.";
        if (!PhoneRx.IsMatch(req.ContactNumber.Trim())) return "Enter a valid contact number.";
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName) || string.IsNullOrWhiteSpace(req.Address))
            return "Name and address are required.";
        return null;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void Apply(Patient p, PatientRequest r, string gender)
    {
        p.FirstName = r.FirstName.Trim();
        p.LastName = r.LastName.Trim();
        p.DateOfBirth = r.DateOfBirth.Date;
        p.Gender = gender;
        p.ContactNumber = r.ContactNumber.Trim();
        p.Address = r.Address.Trim();
        p.Email = Clean(r.Email);
        p.EmergencyContact = Clean(r.EmergencyContact);
        p.Allergies = Clean(r.Allergies);
        p.MedicalHistory = Clean(r.MedicalHistory);
    }
}
