using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Services;

public class AvailabilityService(ClinicDbContext db, IClinicClock clock)
{
    public static HashSet<DayOfWeek> ParseDays(string workDays) =>
        workDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : -1)
            .Where(n => n is >= 0 and <= 6)
            .Select(n => (DayOfWeek)n)
            .ToHashSet();

    /// <summary>Every slot of the doctor's working day, flagged available when it is in the future and not booked.</summary>
    public async Task<List<SlotDto>> GetSlotsAsync(Doctor doctor, DateTime date, int? ignoreAppointmentId = null)
    {
        date = date.Date;
        var slots = new List<SlotDto>();
        if (!ParseDays(doctor.WorkDays).Contains(date.DayOfWeek) || doctor.SlotMinutes <= 0) return slots;

        var dayEnd = date.AddDays(1);
        var ignoreId = ignoreAppointmentId ?? 0;
        var taken = (await db.Appointments
                .Where(a => a.DoctorId == doctor.Id && a.StartsAt >= date && a.StartsAt < dayEnd && !a.IsWalkIn
                            && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow
                            && a.Id != ignoreId)
                .Select(a => a.StartsAt)
                .ToListAsync())
            .ToHashSet();

        var step = TimeSpan.FromMinutes(doctor.SlotMinutes);
        var now = clock.Now;
        for (var t = doctor.StartTime; t + step <= doctor.EndTime; t += step)
        {
            var start = date + t;
            slots.Add(new SlotDto(start, start >= now && !taken.Contains(start)));
        }
        return slots;
    }

    public async Task<bool> IsAvailableAsync(Doctor doctor, DateTime startsAt, int? ignoreAppointmentId = null)
    {
        var slots = await GetSlotsAsync(doctor, startsAt.Date, ignoreAppointmentId);
        return slots.Any(s => s.Start == startsAt && s.Available);
    }
}
