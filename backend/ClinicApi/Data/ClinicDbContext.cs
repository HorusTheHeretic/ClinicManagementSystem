using ClinicApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Data;

public class ClinicDbContext(DbContextOptions<ClinicDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<ClinicService> Services => Set<ClinicService>();
    public DbSet<ConsultationService> ConsultationServices => Set<ConsultationService>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(50);
            e.Property(x => x.PasswordHash).HasMaxLength(100);
            e.Property(x => x.FullName).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(120);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Doctor>(e =>
        {
            e.HasOne(x => x.User).WithOne(x => x.Doctor).HasForeignKey<Doctor>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.Specialty).HasMaxLength(80);
            e.Property(x => x.WorkDays).HasMaxLength(20);
            e.Property(x => x.ConsultationFee).HasColumnType("decimal(10,2)");
        });

        b.Entity<Patient>(e =>
        {
            e.HasIndex(x => x.PatientNo).IsUnique();
            e.HasIndex(x => new { x.LastName, x.FirstName });
            e.Property(x => x.PatientNo).HasMaxLength(20);
            e.Property(x => x.FirstName).HasMaxLength(60);
            e.Property(x => x.LastName).HasMaxLength(60);
            e.Property(x => x.DateOfBirth).HasColumnType("date");
            e.Property(x => x.Gender).HasMaxLength(10);
            e.Property(x => x.ContactNumber).HasMaxLength(30);
            e.Property(x => x.Address).HasMaxLength(250);
            e.Property(x => x.Email).HasMaxLength(120);
            e.Property(x => x.EmergencyContact).HasMaxLength(150);
            e.Property(x => x.Allergies).HasMaxLength(500);
            e.Property(x => x.MedicalHistory).HasMaxLength(2000);
        });

        b.Entity<Appointment>(e =>
        {
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.AppointmentNo).IsUnique();
            e.HasIndex(x => new { x.DoctorId, x.StartsAt });
            e.HasIndex(x => x.SlotKey).IsUnique();
            e.Property(x => x.SlotKey).HasMaxLength(40);
            // One queue number per doctor per day. NULL queue numbers (not checked in yet) never collide.
            e.HasIndex(x => new { x.DoctorId, x.QueueDate, x.QueueNumber }).IsUnique();
            e.Property(x => x.AppointmentNo).HasMaxLength(20);
            e.Property(x => x.Reason).HasMaxLength(300);
            e.Property(x => x.CancelReason).HasMaxLength(300);
            e.Property(x => x.QueueDate).HasColumnType("date");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(10);
        });

        b.Entity<Consultation>(e =>
        {
            e.HasOne(x => x.Appointment).WithOne().HasForeignKey<Consultation>(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.AppointmentId).IsUnique();
            e.HasIndex(x => x.PatientId);
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Symptoms).HasMaxLength(2000);
            e.Property(x => x.BloodPressure).HasMaxLength(15);
            e.Property(x => x.TemperatureC).HasColumnType("decimal(4,1)");
            e.Property(x => x.WeightKg).HasColumnType("decimal(5,1)");
            e.Property(x => x.Diagnosis).HasMaxLength(1000);
            e.Property(x => x.Treatment).HasMaxLength(2000);
            e.Property(x => x.Prescription).HasMaxLength(2000);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.FollowUpDate).HasColumnType("date");
        });

        b.Entity<ClinicService>(e =>
        {
            e.ToTable("Services");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Fee).HasColumnType("decimal(10,2)");
        });

        b.Entity<ConsultationService>(e =>
        {
            e.HasOne<Consultation>().WithMany(c => c.Services).HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.UnitPrice).HasColumnType("decimal(10,2)");
        });

        b.Entity<Invoice>(e =>
        {
            e.HasOne<Consultation>().WithOne().HasForeignKey<Invoice>(x => x.ConsultationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.InvoiceNo).IsUnique();
            e.HasIndex(x => x.ConsultationId).IsUnique();
            e.Property(x => x.InvoiceNo).HasMaxLength(20);
            e.Property(x => x.Total).HasColumnType("decimal(10,2)");
            // Optimistic concurrency: two cashiers cannot apply payments to the same invoice balance at once.
            e.Property(x => x.AmountPaid).HasColumnType("decimal(10,2)").IsConcurrencyToken();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<InvoiceItem>(e =>
        {
            e.HasOne<Invoice>().WithMany(i => i.Items).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Description).HasMaxLength(200);
            e.Property(x => x.UnitPrice).HasColumnType("decimal(10,2)");
            e.Property(x => x.LineTotal).HasColumnType("decimal(10,2)");
        });

        b.Entity<Payment>(e =>
        {
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReceivedBy).WithMany().HasForeignKey(x => x.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ReceiptNo).IsUnique();
            e.Property(x => x.ReceiptNo).HasMaxLength(20);
            e.Property(x => x.AmountTendered).HasColumnType("decimal(10,2)");
            e.Property(x => x.AmountApplied).HasColumnType("decimal(10,2)");
            e.Property(x => x.ChangeGiven).HasColumnType("decimal(10,2)");
            e.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Reference).HasMaxLength(80);
        });
    }
}
