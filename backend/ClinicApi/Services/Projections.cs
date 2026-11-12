using ClinicApi.Dtos;
using ClinicApi.Models;

namespace ClinicApi.Services;

public static class Projections
{
    public static IQueryable<AppointmentDto> ToAppointmentDto(this IQueryable<Appointment> q) =>
        q.Select(a => new AppointmentDto(
            a.Id, a.AppointmentNo, a.PatientId, a.Patient.FirstName + " " + a.Patient.LastName, a.Patient.PatientNo,
            a.DoctorId, a.Doctor.User.FullName, a.Doctor.Specialty, a.StartsAt, a.DurationMinutes, a.Reason,
            a.Status, a.IsWalkIn, a.Priority, a.QueueNumber, a.CheckedInAt));

    public static int AgeOn(DateTime dateOfBirth, DateTime today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age)) age--;
        return Math.Max(age, 0);
    }

    public static PatientDto ToDto(this Patient p, DateTime today) => new(
        p.Id, p.PatientNo, p.FirstName, p.LastName, p.DateOfBirth, AgeOn(p.DateOfBirth, today), p.Gender,
        p.ContactNumber, p.Address, p.Email, p.EmergencyContact, p.Allergies, p.MedicalHistory, p.CreatedAt);

    public static string SlotKeyFor(int doctorId, DateTime startsAt) => $"{doctorId}:{startsAt:yyyyMMddHHmm}";
}
