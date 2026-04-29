USE LeaveDb;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Returns balance per leave type for an employee in a given year.
-- Approved + Pending days reduce the displayed remaining count.
CREATE OR ALTER PROCEDURE sp_GetLeaveBalance
    @EmployeeId INT,
    @Year       INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @YearStart DATE = DATEFROMPARTS(@Year, 1, 1);
    DECLARE @YearEnd   DATE = DATEFROMPARTS(@Year, 12, 31);

    SELECT
        lt.Id              AS LeaveTypeId,
        lt.Name            AS LeaveTypeName,
        lt.IsPaid,
        ISNULL(q.AnnualDays, lt.DefaultAnnualQuota) AS AnnualDays,
        ISNULL((
            SELECT SUM(DATEDIFF(DAY, lr.StartDate, lr.EndDate) + 1)
            FROM LeaveRequests lr
            WHERE lr.EmployeeId  = @EmployeeId
              AND lr.LeaveTypeId = lt.Id
              AND lr.Status      = 'Approved'
              AND lr.StartDate >= @YearStart
              AND lr.EndDate   <= @YearEnd
        ), 0) AS UsedDays,
        ISNULL((
            SELECT SUM(DATEDIFF(DAY, lr.StartDate, lr.EndDate) + 1)
            FROM LeaveRequests lr
            WHERE lr.EmployeeId  = @EmployeeId
              AND lr.LeaveTypeId = lt.Id
              AND lr.Status      = 'Pending'
              AND lr.StartDate >= @YearStart
              AND lr.EndDate   <= @YearEnd
        ), 0) AS PendingDays
    FROM LeaveTypes lt
    ORDER BY lt.Name;
END
GO

-- Recursive descendants of a manager (direct + indirect reports).
CREATE OR ALTER PROCEDURE sp_GetTeamHierarchy
    @ManagerId INT
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Tree AS (
        SELECT e.Id, e.UserId, e.FullName, e.DepartmentId, e.Position, e.HireDate, e.ManagerId, 1 AS Depth
        FROM Employees e WHERE e.ManagerId = @ManagerId
        UNION ALL
        SELECT e.Id, e.UserId, e.FullName, e.DepartmentId, e.Position, e.HireDate, e.ManagerId, t.Depth + 1
        FROM Employees e INNER JOIN Tree t ON e.ManagerId = t.Id
    )
    SELECT t.*, d.Name AS DepartmentName
    FROM Tree t INNER JOIN Departments d ON d.Id = t.DepartmentId
    ORDER BY t.Depth, t.FullName;
END
GO

-- Approve a leave request: changes status, writes audit, returns 1 on success.
CREATE OR ALTER PROCEDURE sp_DecideLeaveRequest
    @RequestId    INT,
    @ActorUserId  INT,
    @NewStatus    NVARCHAR(20),
    @Note         NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @OldStatus NVARCHAR(20);

    BEGIN TRAN;
    SELECT @OldStatus = Status FROM LeaveRequests WITH (UPDLOCK, HOLDLOCK) WHERE Id = @RequestId;

    IF @OldStatus IS NULL
    BEGIN
        ROLLBACK TRAN;
        THROW 51001, 'Leave request not found.', 1;
    END

    IF @OldStatus <> 'Pending'
    BEGIN
        ROLLBACK TRAN;
        THROW 51002, 'Only Pending requests can be decided.', 1;
    END

    UPDATE LeaveRequests
    SET Status          = @NewStatus,
        DecidedAt       = GETUTCDATE(),
        DecidedByUserId = @ActorUserId,
        DecisionNote    = @Note
    WHERE Id = @RequestId;

    INSERT INTO AuditLog (EntityType, EntityId, ActorUserId, Action, OldValue, NewValue, Notes)
    VALUES ('LeaveRequest', @RequestId, @ActorUserId, 'StatusChange', @OldStatus, @NewStatus, @Note);

    COMMIT TRAN;
    SELECT 1 AS Success;
END
GO

-- HR dashboard: headcount + on-leave today + per-type usage (current year).
CREATE OR ALTER PROCEDURE sp_GetHrDashboard
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Today DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @YearStart DATE = DATEFROMPARTS(YEAR(@Today), 1, 1);

    -- result 1: headcount per department
    SELECT d.Id, d.Name, COUNT(e.Id) AS Headcount
    FROM Departments d LEFT JOIN Employees e ON e.DepartmentId = d.Id
    GROUP BY d.Id, d.Name
    ORDER BY d.Name;

    -- result 2: on leave today
    SELECT e.Id, e.FullName, d.Name AS DepartmentName, lt.Name AS LeaveTypeName,
           lr.StartDate, lr.EndDate
    FROM LeaveRequests lr
    INNER JOIN Employees e   ON e.Id  = lr.EmployeeId
    INNER JOIN Departments d ON d.Id  = e.DepartmentId
    INNER JOIN LeaveTypes lt ON lt.Id = lr.LeaveTypeId
    WHERE lr.Status = 'Approved' AND @Today BETWEEN lr.StartDate AND lr.EndDate
    ORDER BY e.FullName;

    -- result 3: usage per leave type this year
    SELECT lt.Id, lt.Name,
           COUNT(lr.Id) AS RequestCount,
           ISNULL(SUM(DATEDIFF(DAY, lr.StartDate, lr.EndDate) + 1), 0) AS TotalDays
    FROM LeaveTypes lt
    LEFT JOIN LeaveRequests lr ON lr.LeaveTypeId = lt.Id
        AND lr.Status = 'Approved'
        AND lr.StartDate >= @YearStart
    GROUP BY lt.Id, lt.Name
    ORDER BY lt.Name;

    -- result 4: top counts
    SELECT
        (SELECT COUNT(*) FROM Employees) AS TotalEmployees,
        (SELECT COUNT(*) FROM Departments) AS TotalDepartments,
        (SELECT COUNT(*) FROM LeaveRequests WHERE Status = 'Pending') AS PendingRequests;
END
GO
