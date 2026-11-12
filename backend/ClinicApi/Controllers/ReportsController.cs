using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

[Route("api/[controller]"), Authorize(Roles = "Admin")]
public class ReportsController(ClinicDbContext db, IClinicClock clock) : ApiBase
{
    private static string D(DateTime d) => d.ToString("yyyy-MM-dd");

    /// <summary>Report types: appointments, revenue, patients, diagnoses (algorithm 1.8).</summary>
    [HttpGet("{type}")]
    public async Task<ActionResult<ReportDto>> Generate(string type, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = (from ?? clock.Today.AddDays(-29)).Date;
        var t = (to ?? clock.Today).Date;
        if (t < f) return Fail("The end date must be on or after the start date.");
        if ((t - f).TotalDays > 366) return Fail("Choose a range of one year or less.");
        var end = t.AddDays(1);

        ReportDto? report = type.ToLowerInvariant() switch
        {
            "appointments" => await AppointmentsReport(f, t, end),
            "revenue" => await RevenueReport(f, t, end),
            "patients" => await PatientsReport(f, t, end),
            "diagnoses" => await DiagnosesReport(f, t, end),
            _ => null,
        };
        return report is null ? NotFound(new { message = "Unknown report type." }) : report;
    }

    private async Task<ReportDto> AppointmentsReport(DateTime f, DateTime t, DateTime end)
    {
        var rows = await db.Appointments.Where(a => a.StartsAt >= f && a.StartsAt < end)
            .Select(a => new { Doctor = a.Doctor.User.FullName, a.Status, a.IsWalkIn }).ToListAsync();
        var byDoctor = rows.GroupBy(r => r.Doctor).OrderBy(g => g.Key).Select(g => new List<object?>
        {
            g.Key, g.Count(),
            g.Count(x => x.Status == AppointmentStatus.Completed),
            g.Count(x => x.Status == AppointmentStatus.Cancelled),
            g.Count(x => x.Status == AppointmentStatus.NoShow),
            g.Count(x => x.IsWalkIn),
        }).ToList();
        var done = rows.Count(r => r.Status == AppointmentStatus.Completed);
        var rate = rows.Count == 0 ? 0 : Math.Round(done * 100.0 / rows.Count, 1);
        return new ReportDto("Appointments by doctor", f, t,
            [new("Doctor", "text"), new("Total", "number"), new("Completed", "number"), new("Cancelled", "number"), new("No-show", "number"), new("Walk-ins", "number")],
            byDoctor,
            [new("Appointments", rows.Count.ToString()), new("Completed", $"{done} ({rate}%)"),
             new("Cancelled", rows.Count(r => r.Status == AppointmentStatus.Cancelled).ToString()),
             new("No-shows", rows.Count(r => r.Status == AppointmentStatus.NoShow).ToString())]);
    }

    private async Task<ReportDto> RevenueReport(DateTime f, DateTime t, DateTime end)
    {
        var billed = await db.Invoices.Where(i => i.CreatedAt >= f && i.CreatedAt < end)
            .Select(i => new { i.CreatedAt, i.Total, Balance = i.Total - i.AmountPaid }).ToListAsync();
        var paid = await db.Payments.Where(p => p.PaidAt >= f && p.PaidAt < end)
            .Select(p => new { p.PaidAt, p.AmountApplied, p.Method }).ToListAsync();

        var days = billed.Select(b => b.CreatedAt.Date).Union(paid.Select(p => p.PaidAt.Date)).OrderBy(d => d).ToList();
        var rows = days.Select(d => new List<object?>
        {
            D(d),
            billed.Count(b => b.CreatedAt.Date == d),
            billed.Where(b => b.CreatedAt.Date == d).Sum(b => b.Total),
            paid.Where(p => p.PaidAt.Date == d).Sum(p => p.AmountApplied),
        }).ToList();

        var summary = new List<ReportSummary>
        {
            new("Billed", $"{billed.Sum(b => b.Total):N2}"),
            new("Collected", $"{paid.Sum(p => p.AmountApplied):N2}"),
            new("Still unpaid (from these invoices)", $"{billed.Sum(b => b.Balance):N2}"),
        };
        foreach (var g in paid.GroupBy(p => p.Method).OrderBy(g => g.Key))
            summary.Add(new($"Collected by {g.Key}", $"{g.Sum(x => x.AmountApplied):N2}"));

        return new ReportDto("Revenue", f, t,
            [new("Date", "date"), new("Invoices", "number"), new("Billed", "money"), new("Collected", "money")], rows, summary);
    }

    private async Task<ReportDto> PatientsReport(DateTime f, DateTime t, DateTime end)
    {
        var list = await db.Patients.Where(p => !p.IsDeleted && p.CreatedAt >= f && p.CreatedAt < end)
            .OrderBy(p => p.CreatedAt).ToListAsync();
        var today = clock.Today;
        var rows = list.Select(p => new List<object?>
        {
            D(p.CreatedAt), p.PatientNo, $"{p.LastName}, {p.FirstName}", Projections.AgeOn(p.DateOfBirth, today), p.Gender, p.ContactNumber,
        }).ToList();
        var totalActive = await db.Patients.CountAsync(p => !p.IsDeleted);
        return new ReportDto("New patient registrations", f, t,
            [new("Registered", "date"), new("Patient no.", "text"), new("Name", "text"), new("Age", "number"), new("Gender", "text"), new("Contact", "text")],
            rows, [new("New patients", list.Count.ToString()), new("All patients on file", totalActive.ToString())]);
    }

    private async Task<ReportDto> DiagnosesReport(DateTime f, DateTime t, DateTime end)
    {
        var diagnoses = await db.Consultations.Where(c => c.CreatedAt >= f && c.CreatedAt < end).Select(c => c.Diagnosis).ToListAsync();
        var groups = diagnoses.GroupBy(d => d.Trim().ToLowerInvariant())
            .Select(g => new { Label = g.First().Trim(), Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Label).ToList();
        return new ReportDto("Diagnoses seen", f, t,
            [new("Diagnosis", "text"), new("Cases", "number")],
            groups.Select(g => new List<object?> { g.Label, g.Count }).ToList(),
            [new("Consultations", diagnoses.Count.ToString()), new("Different diagnoses", groups.Count.ToString())]);
    }
}
