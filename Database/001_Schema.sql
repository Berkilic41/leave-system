USE master;
GO
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'LeaveDb')
BEGIN
    ALTER DATABASE LeaveDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE LeaveDb;
END
CREATE DATABASE LeaveDb;
GO
USE LeaveDb;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE TABLE Users (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL,
    Email        NVARCHAR(150) NOT NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    PasswordSalt NVARCHAR(512) NOT NULL,
    Role         NVARCHAR(20)  NOT NULL DEFAULT 'Employee',
    IsActive     BIT           NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT UQ_Users_Email    UNIQUE (Email),
    CONSTRAINT CK_Users_Role     CHECK (Role IN ('HR','Manager','Employee'))
);

CREATE TABLE Departments (
    Id   INT           IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    CONSTRAINT UQ_Departments_Name UNIQUE (Name)
);

CREATE TABLE Employees (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    UserId       INT           NOT NULL,
    FullName     NVARCHAR(150) NOT NULL,
    DepartmentId INT           NOT NULL,
    Position     NVARCHAR(150) NOT NULL,
    HireDate     DATE          NOT NULL,
    ManagerId    INT           NULL,
    CONSTRAINT UQ_Employees_User UNIQUE (UserId),
    CONSTRAINT FK_Emp_User    FOREIGN KEY (UserId)       REFERENCES Users(Id),
    CONSTRAINT FK_Emp_Dept    FOREIGN KEY (DepartmentId) REFERENCES Departments(Id),
    CONSTRAINT FK_Emp_Manager FOREIGN KEY (ManagerId)    REFERENCES Employees(Id)
);
CREATE INDEX IX_Emp_Manager ON Employees(ManagerId);
CREATE INDEX IX_Emp_Dept    ON Employees(DepartmentId);

CREATE TABLE LeaveTypes (
    Id                 INT          IDENTITY(1,1) PRIMARY KEY,
    Name               NVARCHAR(50) NOT NULL,
    DefaultAnnualQuota INT          NOT NULL,
    IsPaid             BIT          NOT NULL DEFAULT 1,
    CONSTRAINT UQ_LeaveTypes_Name UNIQUE (Name)
);

-- Per-employee per-year overrides (optional; missing rows fall back to DefaultAnnualQuota)
CREATE TABLE LeaveQuotas (
    EmployeeId  INT NOT NULL,
    LeaveTypeId INT NOT NULL,
    [Year]      INT NOT NULL,
    AnnualDays  INT NOT NULL,
    CONSTRAINT PK_Quotas PRIMARY KEY (EmployeeId, LeaveTypeId, [Year]),
    CONSTRAINT FK_Q_Emp  FOREIGN KEY (EmployeeId)  REFERENCES Employees(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Q_Type FOREIGN KEY (LeaveTypeId) REFERENCES LeaveTypes(Id)
);

CREATE TABLE LeaveRequests (
    Id              INT           IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT           NOT NULL,
    LeaveTypeId     INT           NOT NULL,
    StartDate       DATE          NOT NULL,
    EndDate         DATE          NOT NULL,
    Reason          NVARCHAR(1000),
    Status          NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
    DecidedAt       DATETIME2,
    DecidedByUserId INT,
    DecisionNote    NVARCHAR(500),
    CreatedAt       DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_LR_Emp   FOREIGN KEY (EmployeeId)      REFERENCES Employees(Id),
    CONSTRAINT FK_LR_Type  FOREIGN KEY (LeaveTypeId)     REFERENCES LeaveTypes(Id),
    CONSTRAINT FK_LR_DecBy FOREIGN KEY (DecidedByUserId) REFERENCES Users(Id),
    CONSTRAINT CK_LR_Range CHECK (StartDate <= EndDate),
    CONSTRAINT CK_LR_Status CHECK (Status IN ('Pending','Approved','Rejected','Cancelled'))
);
CREATE INDEX IX_LR_Employee_Range ON LeaveRequests(EmployeeId, StartDate, EndDate);
CREATE INDEX IX_LR_Status_Start    ON LeaveRequests(Status, StartDate);

CREATE TABLE AuditLog (
    Id          INT            IDENTITY(1,1) PRIMARY KEY,
    EntityType  NVARCHAR(50)   NOT NULL,
    EntityId    INT            NOT NULL,
    ActorUserId INT            NOT NULL,
    Action      NVARCHAR(50)   NOT NULL,
    OldValue    NVARCHAR(500),
    NewValue    NVARCHAR(500),
    Notes       NVARCHAR(1000),
    [Timestamp] DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Audit_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id)
);
CREATE INDEX IX_Audit_Entity ON AuditLog(EntityType, EntityId, [Timestamp] DESC);
GO
