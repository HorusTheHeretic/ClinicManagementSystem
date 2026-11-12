using ClinicApi.Data;
using ClinicApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Services;

public class QueueService(ClinicDbContext db, IClinicClock clock)
{
    /// <summary>
    /// Puts an appointment into today's waiting line: assigns the doctor's next queue number
    /// and sets the status to Waiting. A unique index on (doctor, day, number) guards against
    /// two desks checking patients in at the same moment, so a collision simply retries.
    /// </summary>
    public async Task CheckInAsync(Appointment a, QueuePriority priority)
    {
        var today = clock.Today;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var max = await db.Appointments
                .Where(x => x.DoctorId == a.DoctorId && x.QueueDate == today)
                .MaxAsync(x => x.QueueNumber) ?? 0;

            a.QueueDate = today;
            a.QueueNumber = max + 1;
            a.Priority = priority;
            a.Status = AppointmentStatus.Waiting;
            a.CheckedInAt = clock.Now;
            a.UpdatedAt = clock.Now;

            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                // queue number taken by a parallel check-in: recompute and retry
            }
        }
    }

    /// <summary>Urgent patients first, then first come, first served by queue number.</summary>
    public Task<Appointment?> NextWaitingAsync(int doctorId)
    {
        var today = clock.Today;
        return db.Appointments
            .Where(a => a.DoctorId == doctorId && a.QueueDate == today && a.Status == AppointmentStatus.Waiting)
            .OrderByDescending(a => a.Priority == QueuePriority.Urgent)
            .ThenBy(a => a.QueueNumber)
            .FirstOrDefaultAsync();
    }
}
