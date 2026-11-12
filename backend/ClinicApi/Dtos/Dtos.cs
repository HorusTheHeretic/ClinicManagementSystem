using System.ComponentModel.DataAnnotations;
using ClinicApi.Models;

namespace ClinicApi.Dtos;

// ---------- Auth & users ----------
public record LoginRequest([Required] string Username, [Required] string Password);

public record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 4), RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Username may only contain letters, numbers, dots, dashes and underscores.")] string Username,
    [Required] string Password,
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [EmailAddress, StringLength(120)] string? Email,
    [Required] string Role,
    [StringLength(80)] string? Specialty);

public record ChangePasswordRequest([Required] string CurrentPassword, [Required] string NewPassword);
public record ResetPasswordRequest([Required] string NewPassword);
public record SetStatusRequest([Required] AccountStatus Status);

public record CreateUserRequest(
    [Required, StringLength(50, MinimumLength = 4), RegularExpression(@"^[A-Za-z0-9._-]+$")] string Username,
    [Required] string Password,
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [EmailAddress, StringLength(120)] string? Email,
    [Required] UserRole Role,
    [StringLength(80)] string? Specialty);

public record UserDto(int Id, string Username, string FullName, string? Email, UserRole Role, AccountStatus Status,
    DateTime CreatedAt, DateTime? LastLoginAt, int? DoctorId, string? Specialty);

public record SessionUser(int Id, string Username, string FullName, UserRole Role, int? DoctorId);
public record LoginResponse(string Token, DateTime ExpiresAt, SessionUser User);

// ---------- Doctors, services ----------
public record DoctorDto(int Id, int UserId, string FullName, string Specialty, string WorkDays,
    string StartTime, string EndTime, int SlotMinutes, decimal ConsultationFee, AccountStatus Status);

public record UpdateDoctorRequest(
    [Required, StringLength(80)] string Specialty,
    [Required] string WorkDays,
    [Required] string StartTime,
    [Required] string EndTime,
    [Range(10, 120)] int SlotMinutes,
    [Range(0.0, 1000000.0)] decimal ConsultationFee);

public record ServiceRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [StringLength(300)] string? Description,
    [Range(0.0, 1000000.0)] decimal Fee,
    bool IsActive = true);

public record SlotDto(DateTime Start, bool Available);

// ---------- Patients ----------
public record PatientRequest(
    [Required, StringLength(60)] string FirstName,
    [Required, StringLength(60)] string LastName,
    [Required] DateTime DateOfBirth,
    [Required] string Gender,
    [Required, StringLength(30)] string ContactNumber,
    [Required, StringLength(250)] string Address,
    [EmailAddress, StringLength(120)] string? Email,
    [StringLength(150)] string? EmergencyContact,
    [StringLength(500)] string? Allergies,
    [StringLength(2000)] string? MedicalHistory);

public record PatientDto(int Id, string PatientNo, string FirstName, string LastName, DateTime DateOfBirth, int Age,
    string Gender, string ContactNumber, string Address, string? Email, string? EmergencyContact,
    string? Allergies, string? MedicalHistory, DateTime CreatedAt);

public record PagedResult<T>(List<T> Items, int Total);

public record HistoryItemDto(int ConsultationId, DateTime Date, string DoctorName, string Symptoms, string Diagnosis,
    string? Treatment, string? Prescription, string? Notes, DateTime? FollowUpDate);

// ---------- Appointments & queue ----------
public record CreateAppointmentRequest([Required] int PatientId, [Required] int DoctorId, [Required] DateTime StartsAt,
    [StringLength(300)] string? Reason, bool Confirm = true);
public record RescheduleRequest([Required] DateTime StartsAt, int? DoctorId);
public record CancelRequest([StringLength(300)] string? Reason);
public record CheckInRequest(QueuePriority Priority = QueuePriority.Normal);
public record WalkInRequest([Required] int PatientId, [Required] int DoctorId, [StringLength(300)] string? Reason,
    QueuePriority Priority = QueuePriority.Normal);

public record AppointmentDto(int Id, string AppointmentNo, int PatientId, string PatientName, string PatientNo,
    int DoctorId, string DoctorName, string Specialty, DateTime StartsAt, int DurationMinutes, string? Reason,
    AppointmentStatus Status, bool IsWalkIn, QueuePriority Priority, int? QueueNumber, DateTime? CheckedInAt);

public record BoardDoctorDto(int DoctorId, string DoctorName, string Specialty, int? NowServing, List<int> Waiting, int WaitingCount);

// ---------- Consultation ----------
public record ConsultationServiceLine([Required] int ServiceId, [Range(1, 99)] int Quantity = 1);

public record ConsultationRequest(
    [Required] int AppointmentId,
    [Required, StringLength(2000)] string Symptoms,
    [StringLength(15)] string? BloodPressure,
    [Range(30.0, 45.0)] decimal? TemperatureC,
    [Range(0.5, 500)] decimal? WeightKg,
    [Required, StringLength(1000)] string Diagnosis,
    [StringLength(2000)] string? Treatment,
    [StringLength(2000)] string? Prescription,
    [StringLength(2000)] string? Notes,
    DateTime? FollowUpDate,
    List<ConsultationServiceLine>? Services);

public record AmendConsultationRequest(
    [Required, StringLength(2000)] string Symptoms,
    [Required, StringLength(1000)] string Diagnosis,
    [StringLength(2000)] string? Treatment,
    [StringLength(2000)] string? Prescription,
    [StringLength(2000)] string? Notes,
    DateTime? FollowUpDate);

// ---------- Billing ----------
public record PayRequest([Range(0.01, 10000000)] decimal Amount, [Required] PaymentMethod Method, [StringLength(80)] string? Reference);

public record InvoiceListDto(int Id, string InvoiceNo, int PatientId, string PatientName, string PatientNo, string DoctorName,
    decimal Total, decimal AmountPaid, decimal Balance, InvoiceStatus Status, DateTime CreatedAt);

public record InvoiceItemDto(string Description, int Quantity, decimal UnitPrice, decimal LineTotal);
public record PaymentSummaryDto(int Id, string ReceiptNo, DateTime PaidAt, decimal AmountApplied, decimal AmountTendered, decimal ChangeGiven, PaymentMethod Method);

public record InvoiceDetailDto(int Id, string InvoiceNo, int PatientId, string PatientName, string PatientNo, string DoctorName,
    decimal Total, decimal AmountPaid, decimal Balance, InvoiceStatus Status, DateTime CreatedAt,
    List<InvoiceItemDto> Items, List<PaymentSummaryDto> Payments);

public record ReceiptDto(string ClinicName, string ClinicAddress, string ReceiptNo, DateTime PaidAt, string InvoiceNo,
    string PatientName, string PatientNo, string DoctorName, List<InvoiceItemDto> Items, decimal InvoiceTotal,
    decimal AmountTendered, decimal AmountApplied, decimal ChangeGiven, decimal BalanceAfter, InvoiceStatus Status,
    PaymentMethod Method, string? Reference, string CashierName);

// ---------- Reports ----------
public record ReportColumn(string Label, string Kind);   // Kind: text | number | money | date
public record ReportSummary(string Label, string Value);
public record ReportDto(string Title, DateTime From, DateTime To, List<ReportColumn> Columns, List<List<object?>> Rows, List<ReportSummary> Summary);
