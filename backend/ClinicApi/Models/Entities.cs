namespace ClinicApi.Models;

// All event timestamps (CreatedAt, CheckedInAt, PaidAt ...) are stored in clinic local time.
// See Services/ClinicClock.cs.

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public UserRole Role { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Pending;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public Doctor? Doctor { get; set; }
}

public class Doctor
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Specialty { get; set; } = "General Medicine";
    /// <summary>Comma separated DayOfWeek numbers, Sunday = 0. Example: "1,2,3,4,5".</summary>
    public string WorkDays { get; set; } = "1,2,3,4,5";
    public TimeSpan StartTime { get; set; } = new(9, 0, 0);
    public TimeSpan EndTime { get; set; } = new(17, 0, 0);
    public int SlotMinutes { get; set; } = 30;
    public decimal ConsultationFee { get; set; } = 500m;
}

public class Patient
{
    public int Id { get; set; }
    public string PatientNo { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "";
    public string ContactNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Email { get; set; }
    public string? EmergencyContact { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistory { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Appointment
{
    public int Id { get; set; }
    public string AppointmentNo { get; set; } = "";
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    /// <summary>"doctorId:yyyyMMddHHmm" while the slot is held; NULL for walk-ins and cancelled / no-show rows. Unique, so a slot can never be double-booked.</summary>
    public string? SlotKey { get; set; }
    public string? Reason { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public bool IsWalkIn { get; set; }
    public QueuePriority Priority { get; set; } = QueuePriority.Normal;
    public DateTime? QueueDate { get; set; }
    public int? QueueNumber { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CancelReason { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Consultation
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public string Symptoms { get; set; } = "";
    public string? BloodPressure { get; set; }
    public decimal? TemperatureC { get; set; }
    public decimal? WeightKg { get; set; }
    public string Diagnosis { get; set; } = "";
    public string? Treatment { get; set; }
    public string? Prescription { get; set; }
    public string? Notes { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ConsultationService> Services { get; set; } = [];
}

/// <summary>Catalog of billable add-on services (lab tests, procedures ...).</summary>
public class ClinicService
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Fee { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ConsultationService
{
    public int Id { get; set; }
    public int ConsultationId { get; set; }
    public int ServiceId { get; set; }
    public ClinicService Service { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    /// <summary>Fee at the time of the consultation, so later price changes do not alter old bills.</summary>
    public decimal UnitPrice { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = "";
    public int ConsultationId { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
    public DateTime CreatedAt { get; set; }
    public List<InvoiceItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}

public class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public string ReceiptNo { get; set; } = "";
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public decimal AmountTendered { get; set; }
    public decimal AmountApplied { get; set; }
    public decimal ChangeGiven { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public int ReceivedByUserId { get; set; }
    public User ReceivedBy { get; set; } = null!;
    public DateTime PaidAt { get; set; }
}
