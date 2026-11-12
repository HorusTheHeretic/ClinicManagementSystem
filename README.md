# Clinic Management System

A web-based clinic system built from the project draft (algorithms 1.1 to 1.8, the conceptual framework and the layered architecture).

| Layer | Technology |
|---|---|
| Frontend | React 18, HTML5, CSS, JavaScript (Vite). Forms, live queue view, role-based dashboards |
| Backend API | ASP.NET Core (`net8.0`), C#, JWT bearer authentication, role-based authorization |
| Data access | EF Core 9 + Pomelo MySQL provider |
| Database | MySQL 8 |

```
ClinicManagementSystem/
  backend/ClinicApi/     ASP.NET Core Web API
  frontend/              React app
  database/schema.sql    MySQL schema (optional, the API can create it itself)
  docker-compose.yml     MySQL for local development
```

## Run it

You need the .NET 8 SDK (or newer), Node.js 18+, and MySQL 8 (or MariaDB 10.6+).

**1. Database.** Either `docker compose up -d` (creates `clinic_db` and the user from `appsettings.json`), or use your own MySQL and run `database/schema.sql`. If you skip the script, the API creates the tables on first start (`Database:AutoCreate`).

**2. API.**
```bash
cd backend/ClinicApi
# Set a real signing key (32+ characters). Never commit it.
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
# Edit ConnectionStrings:Default in appsettings.json if your MySQL login differs.
dotnet run            # http://localhost:5080
```
For MariaDB, set `Database:ServerVersion` to something like `10.11.0-mariadb`.

**3. Web app.**
```bash
cd frontend
npm install
npm run dev           # http://localhost:5173  (proxies /api to the API on :5080)
```

On first start the API creates an administrator and demo data (`Seed` section of `appsettings.json`):

| Role | Username | Password |
|---|---|---|
| Admin | `admin` | `Admin@12345` |
| Staff | `staff` | `Staff@12345` |
| Doctor | `dr.reyes`, `dr.santos` | `Doctor@12345` |

**Change these passwords, or set `Seed:DemoData` to `false`, before real use.**

## Roles

| | Admin | Staff (front desk) | Doctor |
|---|---|---|---|
| Approve sign-ups, manage users, services, fees, doctor schedules | yes | | own schedule |
| Register and edit patients | yes | yes | read only |
| Delete patients (soft delete) | yes | | |
| Patient medical history | yes | | yes |
| Book, confirm, reschedule, cancel appointments | yes | yes | view own |
| Check in, add walk-ins, manage the line | yes | yes | |
| Call next patient, record consultation | | | yes |
| Billing and receipts | yes | yes | |
| Reports | yes | | |

## How the draft maps to the code

| Draft | Where |
|---|---|
| 1.1 Login and registration | `AuthController`. Sign-up lets a person choose Staff or Doctor. The account stays **Pending** until an admin approves it (nobody can self-register as Admin). Passwords are BCrypt-hashed. Five wrong passwords lock the account for 15 minutes. |
| 1.2 Patient registration | `PatientsController.Create`. Validates, looks for an existing record (same name and birth date, returns it), then generates `PT-2026-00001`. |
| 1.3 Record management | `PatientsController` search, update, soft delete with confirmation. |
| 1.4 Appointments | `AppointmentsController` + `AvailabilityService`. Slots come from each doctor's working days, hours and visit length. A unique `SlotKey` makes double-booking impossible even when two desks book at once. |
| 1.5 Queue | `QueueService` + `QueueController`. Check-in assigns the doctor's next queue number for the day. Calling the next patient picks urgent first, then lowest number, and sets "In Consultation". `/board` is a public waiting-room display showing numbers only. |
| 1.6 Consultation | `ConsultationsController`. One transaction saves the record, marks the appointment Completed and creates the invoice. |
| 1.7 Billing | `BillingController`. Partial payments keep the invoice open and return the remaining balance. Full payment works out change (cash only). Receipts are printable. |
| 1.8 Reports | `ReportsController`: appointments by doctor, revenue, new patients, diagnoses. Print or save as CSV. |

Added beyond the draft: walk-ins, urgent priority, public queue display, doctor schedules, a service catalog, optimistic concurrency on payments, immediate lockout of disabled accounts, and a role-based dashboard.

## Notes

- **Time zone.** Scheduling follows the clinic's wall clock (`Clinic:TimeZoneId`, default `Asia/Manila`), not the server's. All stored timestamps are clinic local time.
- **Currency** is shown as PHP (`peso()` in `frontend/src/utils.js`). Change it there.
- **Production.** Serve over HTTPS, set `Cors:Origins` to your site, supply `Jwt__Key` and the connection string as environment variables, run `npm run build`, and set `VITE_API_URL` if the API is on another address.
- **Migrations.** The project uses `EnsureCreated` for a zero-setup start. For a long-lived database, switch to EF migrations (`dotnet ef migrations add Initial`).
