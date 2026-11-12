using ClinicApi.Data;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

/// <summary>One endpoint, three shapes: each role gets the numbers and worklists it acts on.</summary>
[Route("api/[controller]"), Authorize]
public class DashboardController(ClinicDbContext db, IClinicClock clock, QueueService queue) : ApiBase
{
    [HttpGet]
    public async Task<ActionResult> Get() => CurrentRole switch
    {
        UserRole.Admin => Ok(await AdminView()),
        UserRole.Staff => Ok(await StaffView()),
        _ => Ok(await DoctorView()),
    };

    private async Task<object> AdminView()
    {
        var today = clock.Today;
        var tomorrow = today.AddDays(1);
        var weekStart = today.AddDays(-6);

        var pending = await db.Users.Where(u => u.Status == AccountStatus.Pending)
            .OrderBy(u => u.CreatedAt).Take(5)
            .Select(u => new { u.Id, u.FullName, u.Role, u.CreatedAt }).ToListAsync();
        var weekDates = await db.Appointments
            .Where(a => a.StartsAt >= weekStart && a.StartsAt < tomorrow && a.Status != AppointmentStatus.Cancelled)
            .Select(a => a.StartsAt).ToListAsync();

        return new
        {
            role = "Admin",
            pendingUsers = await db.Users.CountAsync(u => u.Status == AccountStatus.Pending),
            activeUsers = await db.Users.CountAsync(u => u.Status == AccountStatus.Active),
            totalPatients = await db.Patients.CountAsync(p => !p.IsDeleted),
            appointmentsToday = await db.Appointments.CountAsync(a => a.StartsAt >= today && a.StartsAt < tomorrow && a.Status != AppointmentStatus.Cancelled),
            waitingNow = await db.Appointments.CountAsync(a => a.QueueDate == today && a.Status == AppointmentStatus.Waiting),
            collectedToday = await db.Payments.Where(p => p.PaidAt >= today && p.PaidAt < tomorrow).SumAsync(p => p.AmountApplied),
            outstanding = await db.Invoices.Where(i => i.Status != InvoiceStatus.Paid).SumAsync(i => i.Total - i.AmountPaid),
            pending,
            trend = Enumerable.Range(0, 7).Select(n => weekStart.AddDays(n))
                .Select(d => new { date = d.ToString("yyyy-MM-dd"), count = weekDates.Count(x => x.Date == d) }),
        };
    }

    private async Task<object> StaffView()
    {
        var today = clock.Today;
        var tomorrow = today.AddDays(1);
        var arrivals = await db.Appointments
            .Where(a => a.StartsAt >= today && a.StartsAt < tomorrow && a.Status == AppointmentStatus.Confirmed)
            .OrderBy(a => a.StartsAt).Take(8).ToAppointmentDto().ToListAsync();

        return new
        {
            role = "Staff",
            appointmentsToday = await db.Appointments.CountAsync(a => a.StartsAt >= today && a.StartsAt < tomorrow && a.Status != AppointmentStatus.Cancelled),
            toConfirm = await db.Appointments.CountAsync(a => a.StartsAt >= today && a.Status == AppointmentStatus.Scheduled),
            waitingNow = await db.Appointments.CountAsync(a => a.QueueDate == today && a.Status == AppointmentStatus.Waiting),
            unpaidInvoices = await db.Invoices.CountAsync(i => i.Status != InvoiceStatus.Paid),
            collectedToday = await db.Payments.Where(p => p.PaidAt >= today && p.PaidAt < tomorrow).SumAsync(p => p.AmountApplied),
            arrivals,
        };
    }

    private async Task<object> DoctorView()
    {
        var doctorId = CurrentDoctorId ?? 0;
        var today = clock.Today;
        var tomorrow = today.AddDays(1);
        var now = clock.Now;

        var current = await db.Appointments.Where(a => a.DoctorId == doctorId && a.QueueDate == today && a.Status == AppointmentStatus.InConsultation)
            .ToAppointmentDto().FirstOrDefaultAsync();
        var nextEntity = await queue.NextWaitingAsync(doctorId);
        var next = nextEntity is null ? null : await db.Appointments.Where(a => a.Id == nextEntity.Id).ToAppointmentDto().FirstAsync();
        var upcoming = await db.Appointments
            .Where(a => a.DoctorId == doctorId && a.StartsAt >= now && a.StartsAt < tomorrow
                        && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.StartsAt).Take(6).ToAppointmentDto().ToListAsync();

        return new
        {
            role = "Doctor",
            appointmentsToday = await db.Appointments.CountAsync(a => a.DoctorId == doctorId && a.StartsAt >= today && a.StartsAt < tomorrow && a.Status != AppointmentStatus.Cancelled),
            waiting = await db.Appointments.CountAsync(a => a.DoctorId == doctorId && a.QueueDate == today && a.Status == AppointmentStatus.Waiting),
            completedToday = await db.Appointments.CountAsync(a => a.DoctorId == doctorId && a.CompletedAt >= today && a.CompletedAt < tomorrow),
            current,
            next,
            upcoming,
        };
    }
}
