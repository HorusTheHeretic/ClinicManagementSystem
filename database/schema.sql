-- Clinic Management System: MySQL 8 schema.
-- Mirrors the EF Core model in backend/ClinicApi, so you can use EITHER this script
-- OR let the API create the tables itself (Database:AutoCreate = true in appsettings.json).
-- The first API start also creates the admin account and, optionally, demo data.

CREATE DATABASE IF NOT EXISTS clinic_db CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE clinic_db;

-- Application login (change the password):
-- CREATE USER IF NOT EXISTS 'clinic_user'@'%' IDENTIFIED BY 'change_me';
-- GRANT ALL PRIVILEGES ON clinic_db.* TO 'clinic_user'@'%';

CREATE TABLE Users (
  Id               INT AUTO_INCREMENT PRIMARY KEY,
  Username         VARCHAR(50)  NOT NULL,
  PasswordHash     VARCHAR(100) NOT NULL,
  FullName         VARCHAR(100) NOT NULL,
  Email            VARCHAR(120) NULL,
  Role             VARCHAR(20)  NOT NULL,            -- Admin | Staff | Doctor
  Status           VARCHAR(20)  NOT NULL,            -- Pending | Active | Disabled
  FailedLoginCount INT          NOT NULL DEFAULT 0,
  LockoutEndUtc    DATETIME(6)  NULL,
  CreatedAt        DATETIME(6)  NOT NULL,
  LastLoginAt      DATETIME(6)  NULL,
  UNIQUE KEY IX_Users_Username (Username)
);

CREATE TABLE Doctors (
  Id              INT AUTO_INCREMENT PRIMARY KEY,
  UserId          INT           NOT NULL,
  Specialty       VARCHAR(80)   NOT NULL,
  WorkDays        VARCHAR(20)   NOT NULL,            -- "1,2,3,4,5" (Sunday = 0)
  StartTime       TIME(6)       NOT NULL,
  EndTime         TIME(6)       NOT NULL,
  SlotMinutes     INT           NOT NULL,
  ConsultationFee DECIMAL(10,2) NOT NULL,
  UNIQUE KEY IX_Doctors_UserId (UserId),
  CONSTRAINT FK_Doctors_Users FOREIGN KEY (UserId) REFERENCES Users (Id)
);

CREATE TABLE Patients (
  Id               INT AUTO_INCREMENT PRIMARY KEY,
  PatientNo        VARCHAR(20)  NOT NULL,
  FirstName        VARCHAR(60)  NOT NULL,
  LastName         VARCHAR(60)  NOT NULL,
  DateOfBirth      DATE         NOT NULL,
  Gender           VARCHAR(10)  NOT NULL,
  ContactNumber    VARCHAR(30)  NOT NULL,
  Address          VARCHAR(250) NOT NULL,
  Email            VARCHAR(120) NULL,
  EmergencyContact VARCHAR(150) NULL,
  Allergies        VARCHAR(500) NULL,
  MedicalHistory   VARCHAR(2000) NULL,
  IsDeleted        TINYINT(1)   NOT NULL DEFAULT 0,
  CreatedAt        DATETIME(6)  NOT NULL,
  UpdatedAt        DATETIME(6)  NOT NULL,
  UNIQUE KEY IX_Patients_PatientNo (PatientNo),
  KEY IX_Patients_LastName_FirstName (LastName, FirstName)
);

CREATE TABLE Appointments (
  Id              INT AUTO_INCREMENT PRIMARY KEY,
  AppointmentNo   VARCHAR(20) NOT NULL,
  PatientId       INT         NOT NULL,
  DoctorId        INT         NOT NULL,
  StartsAt        DATETIME(6) NOT NULL,
  DurationMinutes INT         NOT NULL,
  SlotKey         VARCHAR(40) NULL,                  -- "doctorId:yyyyMMddHHmm" while the slot is held
  Reason          VARCHAR(300) NULL,
  Status          VARCHAR(20) NOT NULL,              -- Scheduled | Confirmed | Waiting | InConsultation | Completed | Cancelled | NoShow
  IsWalkIn        TINYINT(1)  NOT NULL DEFAULT 0,
  Priority        VARCHAR(10) NOT NULL,              -- Normal | Urgent
  QueueDate       DATE        NULL,
  QueueNumber     INT         NULL,
  CheckedInAt     DATETIME(6) NULL,
  CalledAt        DATETIME(6) NULL,
  CompletedAt     DATETIME(6) NULL,
  CancelReason    VARCHAR(300) NULL,
  CreatedByUserId INT         NULL,
  CreatedAt       DATETIME(6) NOT NULL,
  UpdatedAt       DATETIME(6) NOT NULL,
  UNIQUE KEY IX_Appointments_AppointmentNo (AppointmentNo),
  UNIQUE KEY IX_Appointments_SlotKey (SlotKey),
  UNIQUE KEY IX_Appointments_Doctor_Queue (DoctorId, QueueDate, QueueNumber),
  KEY IX_Appointments_Doctor_StartsAt (DoctorId, StartsAt),
  KEY IX_Appointments_PatientId (PatientId),
  CONSTRAINT FK_Appointments_Patients FOREIGN KEY (PatientId) REFERENCES Patients (Id),
  CONSTRAINT FK_Appointments_Doctors  FOREIGN KEY (DoctorId)  REFERENCES Doctors (Id)
);

CREATE TABLE Consultations (
  Id            INT AUTO_INCREMENT PRIMARY KEY,
  AppointmentId INT           NOT NULL,
  PatientId     INT           NOT NULL,
  DoctorId      INT           NOT NULL,
  Symptoms      VARCHAR(2000) NOT NULL,
  BloodPressure VARCHAR(15)   NULL,
  TemperatureC  DECIMAL(4,1)  NULL,
  WeightKg      DECIMAL(5,1)  NULL,
  Diagnosis     VARCHAR(1000) NOT NULL,
  Treatment     VARCHAR(2000) NULL,
  Prescription  VARCHAR(2000) NULL,
  Notes         VARCHAR(2000) NULL,
  FollowUpDate  DATE          NULL,
  CreatedAt     DATETIME(6)   NOT NULL,
  UpdatedAt     DATETIME(6)   NOT NULL,
  UNIQUE KEY IX_Consultations_AppointmentId (AppointmentId),
  KEY IX_Consultations_PatientId (PatientId),
  KEY IX_Consultations_DoctorId (DoctorId),
  CONSTRAINT FK_Consultations_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments (Id),
  CONSTRAINT FK_Consultations_Patients     FOREIGN KEY (PatientId)     REFERENCES Patients (Id),
  CONSTRAINT FK_Consultations_Doctors      FOREIGN KEY (DoctorId)      REFERENCES Doctors (Id)
);

CREATE TABLE Services (
  Id          INT AUTO_INCREMENT PRIMARY KEY,
  Name        VARCHAR(100)  NOT NULL,
  Description VARCHAR(300)  NULL,
  Fee         DECIMAL(10,2) NOT NULL,
  IsActive    TINYINT(1)    NOT NULL DEFAULT 1
);

CREATE TABLE ConsultationServices (
  Id             INT AUTO_INCREMENT PRIMARY KEY,
  ConsultationId INT           NOT NULL,
  ServiceId      INT           NOT NULL,
  Quantity       INT           NOT NULL,
  UnitPrice      DECIMAL(10,2) NOT NULL,
  KEY IX_ConsultationServices_ConsultationId (ConsultationId),
  KEY IX_ConsultationServices_ServiceId (ServiceId),
  CONSTRAINT FK_ConsultationServices_Consultations FOREIGN KEY (ConsultationId) REFERENCES Consultations (Id) ON DELETE CASCADE,
  CONSTRAINT FK_ConsultationServices_Services      FOREIGN KEY (ServiceId)      REFERENCES Services (Id)
);

CREATE TABLE Invoices (
  Id             INT AUTO_INCREMENT PRIMARY KEY,
  InvoiceNo      VARCHAR(20)   NOT NULL,
  ConsultationId INT           NOT NULL,
  PatientId      INT           NOT NULL,
  DoctorId       INT           NOT NULL,
  Total          DECIMAL(10,2) NOT NULL,
  AmountPaid     DECIMAL(10,2) NOT NULL,
  Status         VARCHAR(20)   NOT NULL,             -- Unpaid | PartiallyPaid | Paid
  CreatedAt      DATETIME(6)   NOT NULL,
  UNIQUE KEY IX_Invoices_InvoiceNo (InvoiceNo),
  UNIQUE KEY IX_Invoices_ConsultationId (ConsultationId),
  KEY IX_Invoices_PatientId (PatientId),
  KEY IX_Invoices_DoctorId (DoctorId),
  CONSTRAINT FK_Invoices_Consultations FOREIGN KEY (ConsultationId) REFERENCES Consultations (Id),
  CONSTRAINT FK_Invoices_Patients      FOREIGN KEY (PatientId)      REFERENCES Patients (Id),
  CONSTRAINT FK_Invoices_Doctors       FOREIGN KEY (DoctorId)       REFERENCES Doctors (Id)
);

CREATE TABLE InvoiceItems (
  Id          INT AUTO_INCREMENT PRIMARY KEY,
  InvoiceId   INT           NOT NULL,
  Description VARCHAR(200)  NOT NULL,
  Quantity    INT           NOT NULL,
  UnitPrice   DECIMAL(10,2) NOT NULL,
  LineTotal   DECIMAL(10,2) NOT NULL,
  KEY IX_InvoiceItems_InvoiceId (InvoiceId),
  CONSTRAINT FK_InvoiceItems_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices (Id) ON DELETE CASCADE
);

CREATE TABLE Payments (
  Id               INT AUTO_INCREMENT PRIMARY KEY,
  ReceiptNo        VARCHAR(20)   NOT NULL,
  InvoiceId        INT           NOT NULL,
  AmountTendered   DECIMAL(10,2) NOT NULL,
  AmountApplied    DECIMAL(10,2) NOT NULL,
  ChangeGiven      DECIMAL(10,2) NOT NULL,
  Method           VARCHAR(20)   NOT NULL,           -- Cash | Card | EWallet
  Reference        VARCHAR(80)   NULL,
  ReceivedByUserId INT           NOT NULL,
  PaidAt           DATETIME(6)   NOT NULL,
  UNIQUE KEY IX_Payments_ReceiptNo (ReceiptNo),
  KEY IX_Payments_InvoiceId (InvoiceId),
  KEY IX_Payments_ReceivedByUserId (ReceivedByUserId),
  CONSTRAINT FK_Payments_Invoices FOREIGN KEY (InvoiceId)        REFERENCES Invoices (Id),
  CONSTRAINT FK_Payments_Users    FOREIGN KEY (ReceivedByUserId) REFERENCES Users (Id)
);
