USE LeaveDb;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @Hash NVARCHAR(512) = 'dNenHFzqIK7wTHP3rNRkWw/tqSBIttAjKbks5Tgt5KVD9Rhdnnwqsbtos28hfQ3dpOGciFK1kHO1PAYqGmSETw==';
DECLARE @Salt NVARCHAR(512) = 'y21nmTHP1Vwrtv6X7V+mLm30Xrh74VS6yVJTPjX6qGQO1qmlAUqyDPEODItndn+hacqZNPjczFgVk7qVBK8oOn3/QUfZgz0tuMJ5Jde9nBzQik2ZW8nEgIctMjS8ypPqqliYaB/CA2FJNmBqoOx7vypsuOmR6C8EyzIOst+sXQw=';

-- All seed users use password "password123"
INSERT INTO Users (Username, Email, PasswordHash, PasswordSalt, Role) VALUES
('hr',       'hr@leave.test',       @Hash, @Salt, 'HR'),
('eng_lead', 'sara@leave.test',     @Hash, @Salt, 'Manager'),
('sales_lead','mike@leave.test',    @Hash, @Salt, 'Manager'),
('alice',    'alice@leave.test',    @Hash, @Salt, 'Employee'),
('bob',      'bob@leave.test',      @Hash, @Salt, 'Employee'),
('carol',    'carol@leave.test',    @Hash, @Salt, 'Employee'),
('dan',      'dan@leave.test',      @Hash, @Salt, 'Employee'),
('eve',      'eve@leave.test',      @Hash, @Salt, 'Employee');

INSERT INTO Departments (Name) VALUES
('Engineering'), ('Sales'), ('Operations'), ('HR');

INSERT INTO LeaveTypes (Name, DefaultAnnualQuota, IsPaid) VALUES
('Annual', 20, 1),
('Sick',   10, 1),
('Unpaid', 30, 0);

-- Employees with manager hierarchy:
-- HR (id 1, no employee record needed but we'll create one)
-- Sara (id 2) - Engineering manager, no manager above
-- Mike (id 3) - Sales manager, no manager above
-- Alice, Bob report to Sara
-- Carol reports to Sara
-- Dan, Eve report to Mike

DECLARE @EngId INT = (SELECT Id FROM Departments WHERE Name = 'Engineering');
DECLARE @SalesId INT = (SELECT Id FROM Departments WHERE Name = 'Sales');
DECLARE @HrId INT = (SELECT Id FROM Departments WHERE Name = 'HR');

INSERT INTO Employees (UserId, FullName, DepartmentId, Position, HireDate) VALUES
(1, 'HR Admin',     @HrId,    'HR Manager',          '2020-01-15');
DECLARE @EmpHr INT = SCOPE_IDENTITY();

INSERT INTO Employees (UserId, FullName, DepartmentId, Position, HireDate) VALUES
(2, 'Sara Wright',  @EngId,   'Engineering Manager', '2018-06-01');
DECLARE @EmpSara INT = SCOPE_IDENTITY();

INSERT INTO Employees (UserId, FullName, DepartmentId, Position, HireDate) VALUES
(3, 'Mike Chen',    @SalesId, 'Sales Manager',       '2019-03-15');
DECLARE @EmpMike INT = SCOPE_IDENTITY();

INSERT INTO Employees (UserId, FullName, DepartmentId, Position, HireDate, ManagerId) VALUES
(4, 'Alice Kim',    @EngId,   'Senior Developer', '2021-08-01', @EmpSara),
(5, 'Bob Patel',    @EngId,   'Developer',        '2022-02-15', @EmpSara),
(6, 'Carol Diaz',   @EngId,   'QA Engineer',      '2023-04-10', @EmpSara),
(7, 'Dan Rossi',    @SalesId, 'Account Exec',     '2022-09-01', @EmpMike),
(8, 'Eve Larsen',   @SalesId, 'Sales Rep',        '2023-11-20', @EmpMike);

-- Historical leave requests (mix of statuses)
DECLARE @AnnualId INT = (SELECT Id FROM LeaveTypes WHERE Name = 'Annual');
DECLARE @SickId   INT = (SELECT Id FROM LeaveTypes WHERE Name = 'Sick');

DECLARE @AliceId INT = (SELECT Id FROM Employees WHERE UserId = 4);
DECLARE @BobId   INT = (SELECT Id FROM Employees WHERE UserId = 5);
DECLARE @CarolId INT = (SELECT Id FROM Employees WHERE UserId = 6);
DECLARE @DanId   INT = (SELECT Id FROM Employees WHERE UserId = 7);

-- Approved past leave
INSERT INTO LeaveRequests (EmployeeId, LeaveTypeId, StartDate, EndDate, Reason, Status, DecidedAt, DecidedByUserId)
VALUES
(@AliceId, @AnnualId, DATEADD(DAY, -60, GETUTCDATE()), DATEADD(DAY, -56, GETUTCDATE()), 'Family vacation', 'Approved', DATEADD(DAY, -65, GETUTCDATE()), 2),
(@AliceId, @SickId,   DATEADD(DAY, -20, GETUTCDATE()), DATEADD(DAY, -19, GETUTCDATE()), 'Flu',             'Approved', DATEADD(DAY, -20, GETUTCDATE()), 2),
(@BobId,   @AnnualId, DATEADD(DAY, -40, GETUTCDATE()), DATEADD(DAY, -38, GETUTCDATE()), 'Long weekend',    'Approved', DATEADD(DAY, -42, GETUTCDATE()), 2),
(@DanId,   @AnnualId, DATEADD(DAY, -30, GETUTCDATE()), DATEADD(DAY, -26, GETUTCDATE()), 'Wedding',         'Approved', DATEADD(DAY, -32, GETUTCDATE()), 3);

-- Currently on leave
INSERT INTO LeaveRequests (EmployeeId, LeaveTypeId, StartDate, EndDate, Reason, Status, DecidedAt, DecidedByUserId)
VALUES
(@CarolId, @AnnualId, DATEADD(DAY, -1, GETUTCDATE()), DATEADD(DAY, 3, GETUTCDATE()), 'Trip to mountains', 'Approved', DATEADD(DAY, -10, GETUTCDATE()), 2);

-- Pending requests (awaiting manager)
INSERT INTO LeaveRequests (EmployeeId, LeaveTypeId, StartDate, EndDate, Reason, Status)
VALUES
(@AliceId, @AnnualId, DATEADD(DAY, 14, GETUTCDATE()), DATEADD(DAY, 18, GETUTCDATE()), 'Conference + holiday', 'Pending'),
(@BobId,   @SickId,   DATEADD(DAY, 1,  GETUTCDATE()), DATEADD(DAY, 2,  GETUTCDATE()), 'Doctor appointment',   'Pending');

-- Rejected request
INSERT INTO LeaveRequests (EmployeeId, LeaveTypeId, StartDate, EndDate, Reason, Status, DecidedAt, DecidedByUserId, DecisionNote)
VALUES
(@DanId,   @AnnualId, DATEADD(DAY, 5,  GETUTCDATE()), DATEADD(DAY, 12, GETUTCDATE()), 'Long break', 'Rejected', DATEADD(DAY, -1, GETUTCDATE()), 3, 'Conflicts with quarterly close.');

-- Audit log entries for the decided ones
INSERT INTO AuditLog (EntityType, EntityId, ActorUserId, Action, OldValue, NewValue, Notes)
SELECT 'LeaveRequest', Id, DecidedByUserId, 'StatusChange', 'Pending', Status, DecisionNote
FROM LeaveRequests WHERE Status IN ('Approved','Rejected');
GO
