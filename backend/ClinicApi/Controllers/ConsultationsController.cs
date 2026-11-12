using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

[Route("api/[controller]"), Authorize(Roles = "Doctor")]
public class ConsultationsController(ClinicDbContext db, IClinicClock clock) : ApiBase
{
    /// <summary>Everything the doctor needs on one screen: the appointment, the full patient record, previous visits and any saved consultation.</summary>
    [HttpGet("context/{appointmentId:int}")]
    public async Task<ActionResult> Context(int appointmentId)
    {
        var appt = await db.Appointments.Include(a => a.Patient).FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appt is null) return NotFound(new { message = "Appointment not found." });
        if (appt.DoctorId != CurrentDoctorId) return Forbid();

        var dto = await db.Appointments.Where(a => a.Id == appointmentId).ToAppointmentDto().FirstAsync();
        var history = await db.Consultations
            .Where(c => c.PatientId == appt.PatientId && c.AppointmentId != appointmentId)
            .OrderByDescending(c => c.CreatedAt).Take(10)
            .Select(c => new HistoryItemDto(c.Id, c.CreatedAt, c.Doctor.User.FullName, c.Symptoms, c.Diagnosis,
                c.Treatment, c.Prescription, c.Notes, c.FollowUpDate))
            .ToListAsync();
        var existing = await db.Consultations.Include(c => c.Services).ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(c => c.AppointmentId == appointmentId);
        var fee = await db.Doctors.Where(d => d.Id == appt.DoctorId).Select(d => d.ConsultationFee).FirstAsync();

        return Ok(new
        {
            appointment = dto,
            patient = appt.Patient.ToDto(clock.Today),
            history,
            consultationFee = fee,
            consultation = existing is null ? null : new
            {
                existing.Id, existing.Symptoms, existing.BloodPressure, existing.TemperatureC, existing.WeightKg,
                existing.Diagnosis, existing.Treatment, existing.Prescription, existing.Notes, existing.FollowUpDate,
                services = existing.Services.Select(s => new { s.ServiceId, s.Service.Name, s.Quantity, s.UnitPrice }),
            },
        });
    }

    /// <summary>
    /// Save the consultation (algorithm 1.6). In one transaction this stores the record,
    /// marks the appointment Completed and raises the invoice that billing will collect (algorithm 1.7).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Complete(ConsultationRequest req)
    {
        var appt = await db.Appointments.Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == req.AppointmentId);
        if (appt is null) return NotFound(new { message = "Appointment not found." });
        if (appt.DoctorId != CurrentDoctorId) return Forbid();
        if (appt.Status != AppointmentStatus.InConsultation)
            return Fail("Call this patient from the queue before recording the consultation.", 409);

        var lines = (req.Services ?? [])
            .GroupBy(l => l.ServiceId).Select(g => new ConsultationServiceLine(g.Key, g.Sum(x => x.Quantity))).ToList();
        var ids = lines.Select(l => l.ServiceId).ToList();
        var catalog = await db.Services.Where(s => ids.Contains(s.Id) && s.IsActive).ToDictionaryAsync(s => s.Id);
        if (catalog.Count != ids.Count) return Fail("One of the selected services is no longer available.");

        var now = clock.Now;
        await using var tx = await db.Database.BeginTransactionAsync();

        var consultation = new Consultation
        {
            AppointmentId = appt.Id,
            PatientId = appt.PatientId,
            DoctorId = appt.DoctorId,
            Symptoms = req.Symptoms.Trim(),
            BloodPressure = Clean(req.BloodPressure),
            TemperatureC = req.TemperatureC,
            WeightKg = req.WeightKg,
            Diagnosis = req.Diagnosis.Trim(),
            Treatment = Clean(req.Treatment),
            Prescription = Clean(req.Prescription),
            Notes = Clean(req.Notes),
            FollowUpDate = req.FollowUpDate?.Date,
            CreatedAt = now,
            UpdatedAt = now,
            Services = lines.Select(l => new ConsultationService
            {
                ServiceId = l.ServiceId,
                Quantity = l.Quantity,
                UnitPrice = catalog[l.ServiceId].Fee,
            }).ToList(),
        };
        db.Consultations.Add(consultation);

        appt.Status = AppointmentStatus.Completed;
        appt.CompletedAt = now;
        appt.UpdatedAt = now;
        await db.SaveChangesAsync();

        var items = new List<InvoiceItem>
        {
            new()
            {
                Description = $"Consultation - {appt.Doctor.User.FullName} ({appt.Doctor.Specialty})",
                Quantity = 1,
                UnitPrice = appt.Doctor.ConsultationFee,
                LineTotal = appt.Doctor.ConsultationFee,
            },
        };
        items.AddRange(lines.Select(l => new InvoiceItem
        {
            Description = catalog[l.ServiceId].Name,
            Quantity = l.Quantity,
            UnitPrice = catalog[l.ServiceId].Fee,
            LineTotal = catalog[l.ServiceId].Fee * l.Quantity,
        }));

        var invoice = new Invoice
        {
            ConsultationId = consultation.Id,
            PatientId = appt.PatientId,
            DoctorId = appt.DoctorId,
            Items = items,
            Total = items.Sum(i => i.LineTotal),
            AmountPaid = 0,
            Status = InvoiceStatus.Unpaid,
            CreatedAt = now,
        };
        await db.AddWithNumberAsync(invoice, (x, n) => x.InvoiceNo = n, x => $"INV-{x.Id:D6}");
        await tx.CommitAsync();

        return StatusCode(201, new { consultationId = consultation.Id, invoiceId = invoice.Id, invoiceNo = invoice.InvoiceNo, total = invoice.Total });
    }

    /// <summary>Correct the clinical notes of a saved consultation. Billed services stay as they were.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult> Amend(int id, AmendConsultationRequest req)
    {
        var c = await db.Consultations.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound(new { message = "Consultation not found." });
        if (c.DoctorId != CurrentDoctorId) return Forbid();
        c.Symptoms = req.Symptoms.Trim();
        c.Diagnosis = req.Diagnosis.Trim();
        c.Treatment = Clean(req.Treatment);
        c.Prescription = Clean(req.Prescription);
        c.Notes = Clean(req.Notes);
        c.FollowUpDate = req.FollowUpDate?.Date;
        c.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return Ok(new { message = "Consultation updated." });
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
