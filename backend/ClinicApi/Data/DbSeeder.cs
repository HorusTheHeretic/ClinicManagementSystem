using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Data;

public static class DbSeeder
{
    /// <summary>
    /// First run only (no users yet): creates the administrator and, when Seed:DemoData is true,
    /// a staff member, two doctors, a service catalog, a few patients and today's appointments.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ClinicDbContext>();
        var cfg = sp.GetRequiredService<IConfiguration>();
        var clock = sp.GetRequiredService<IClinicClock>();
        var queue = sp.GetRequiredService<QueueService>();

        if (await db.Users.AnyAsync()) return;

        var now = clock.Now;
        User NewUser(string username, string password, string fullName, UserRole role) => new()
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = fullName,
            Role = role,
            Status = AccountStatus.Active,
            CreatedAt = now,
        };

        db.Users.Add(NewUser(cfg["Seed:AdminUsername"] ?? "admin", cfg["Seed:AdminPassword"] ?? "Admin@12345", "System Administrator", UserRole.Admin));
        await db.SaveChangesAsync();
        if (!cfg.GetValue("Seed:DemoData", false)) return;

        // ----- staff and doctors -----
        db.Users.Add(NewUser("staff", "Staff@12345", "Grace Ramos", UserRole.Staff));
        var reyes = NewUser("dr.reyes", "Doctor@12345", "Dr. Miguel Reyes", UserRole.Doctor);
        reyes.Doctor = new Doctor { Specialty = "General Medicine", ConsultationFee = 500m };
        var santos = NewUser("dr.santos", "Doctor@12345", "Dr. Elena Santos", UserRole.Doctor);
        santos.Doctor = new Doctor { Specialty = "Pediatrics", WorkDays = "1,2,3,4,5,6", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(15, 0, 0), ConsultationFee = 600m };
        db.Users.AddRange(reyes, santos);

        // ----- billable services -----
        db.Services.AddRange(
            new ClinicService { Name = "Complete blood count (CBC)", Fee = 350m },
            new ClinicService { Name = "Urinalysis", Fee = 150m },
            new ClinicService { Name = "Chest X-ray", Fee = 800m },
            new ClinicService { Name = "ECG", Fee = 600m },
            new ClinicService { Name = "Flu vaccination", Fee = 900m },
            new ClinicService { Name = "Wound dressing", Fee = 250m },
            new ClinicService { Name = "Nebulization", Fee = 300m });
        await db.SaveChangesAsync();

        // ----- patients -----
        var people = new (string First, string Last, int Y, int M, int D, string Gender, string Phone, string Address, string? Allergies)[]
        {
            ("Maria", "Santos", 1988, 3, 14, "Female", "0917 123 4567", "12 Mabini St., Davao City", "Penicillin"),
            ("Juan", "Dela Cruz", 1975, 9, 2, "Male", "0918 222 3344", "45 Rizal Ave., Davao City", null),
            ("Ana", "Reyes", 2016, 6, 21, "Female", "0905 555 1212", "8 Bonifacio St., Davao City", null),
            ("Pedro", "Bautista", 1961, 12, 5, "Male", "0927 888 9090", "77 Quezon Blvd., Davao City", "Aspirin"),
            ("Liza", "Mendoza", 1994, 1, 30, "Female", "0999 111 7788", "3 Lapu-Lapu St., Davao City", null),
            ("Carlo", "Villanueva", 2001, 7, 9, "Male", "0936 444 2020", "19 Roxas Ave., Davao City", null),
        };
        var patients = new List<Patient>();
        foreach (var x in people)
        {
            var p = new Patient
            {
                FirstName = x.First, LastName = x.Last, DateOfBirth = new DateTime(x.Y, x.M, x.D), Gender = x.Gender,
                ContactNumber = x.Phone, Address = x.Address, Allergies = x.Allergies, CreatedAt = now, UpdatedAt = now,
            };
            await db.AddWithNumberAsync(p, (e, n) => e.PatientNo = n, e => $"PT-{now.Year}-{e.Id:D5}");
            patients.Add(p);
        }

        // ----- today's appointments, so the queue and dashboards have something to show -----
        var today = clock.Today;
        async Task<Appointment> Book(Patient patient, Doctor doctor, TimeSpan at, AppointmentStatus status, string reason)
        {
            var starts = today + at;
            var a = new Appointment
            {
                PatientId = patient.Id, DoctorId = doctor.Id, StartsAt = starts, DurationMinutes = doctor.SlotMinutes,
                SlotKey = Projections.SlotKeyFor(doctor.Id, starts), Reason = reason, Status = status, CreatedAt = now, UpdatedAt = now,
            };
            await db.AddWithNumberAsync(a, (e, n) => e.AppointmentNo = n, e => $"APT-{e.Id:D6}");
            return a;
        }

        var checkedIn = await Book(patients[0], reyes.Doctor!, new TimeSpan(9, 0, 0), AppointmentStatus.Confirmed, "Persistent cough for one week");
        await queue.CheckInAsync(checkedIn, QueuePriority.Normal);
        await Book(patients[1], reyes.Doctor!, new TimeSpan(9, 30, 0), AppointmentStatus.Confirmed, "Blood pressure check-up");
        await Book(patients[3], reyes.Doctor!, new TimeSpan(10, 0, 0), AppointmentStatus.Scheduled, "Follow-up: hypertension");
        await Book(patients[2], santos.Doctor!, new TimeSpan(8, 30, 0), AppointmentStatus.Confirmed, "Fever and sore throat");
    }
}
