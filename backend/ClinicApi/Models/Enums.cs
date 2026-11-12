namespace ClinicApi.Models;

public enum UserRole { Admin, Staff, Doctor }
public enum AccountStatus { Pending, Active, Disabled }
public enum AppointmentStatus { Scheduled, Confirmed, Waiting, InConsultation, Completed, Cancelled, NoShow }
public enum QueuePriority { Normal, Urgent }
public enum InvoiceStatus { Unpaid, PartiallyPaid, Paid }
public enum PaymentMethod { Cash, Card, EWallet }
