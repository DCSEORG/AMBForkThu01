-- Stored Procedures for Expense Management System
-- All app code uses these stored procedures instead of direct SQL

SET NOCOUNT ON;
GO

-- Get Dashboard Summary
CREATE OR ALTER PROCEDURE GetDashboardSummary
AS
BEGIN
    SELECT 
        (SELECT COUNT(*) FROM dbo.Expenses) AS TotalExpenses,
        (SELECT COUNT(*) FROM dbo.Expenses e JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId WHERE s.StatusName = 'Submitted') AS PendingApprovals,
        ISNULL((SELECT SUM(AmountMinor) / 100.0 FROM dbo.Expenses e JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId WHERE s.StatusName = 'Approved'), 0) AS ApprovedAmount,
        (SELECT COUNT(*) FROM dbo.Expenses e JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId WHERE s.StatusName = 'Approved') AS ApprovedCount
END
GO

-- Get Expenses with optional filtering
CREATE OR ALTER PROCEDURE GetExpenses
    @Filter NVARCHAR(100) = NULL,
    @StatusId INT = NULL
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE (@Filter IS NULL OR 
           e.Description LIKE '%' + @Filter + '%' OR 
           c.CategoryName LIKE '%' + @Filter + '%' OR
           u.UserName LIKE '%' + @Filter + '%')
    AND (@StatusId IS NULL OR e.StatusId = @StatusId)
    ORDER BY e.ExpenseDate DESC
END
GO

-- Get Expense by ID
CREATE OR ALTER PROCEDURE GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE e.ExpenseId = @ExpenseId
END
GO

-- Get Pending Approvals (Submitted expenses)
CREATE OR ALTER PROCEDURE GetPendingApprovals
AS
BEGIN
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users u ON e.UserId = u.UserId
    JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE s.StatusName = 'Submitted'
    ORDER BY e.SubmittedAt ASC
END
GO

-- Create Expense
CREATE OR ALTER PROCEDURE CreateExpense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL
AS
BEGIN
    DECLARE @DraftStatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft')
    
    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, CreatedAt)
    VALUES (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, 'GBP', @ExpenseDate, @Description, SYSUTCDATETIME())
    
    SELECT SCOPE_IDENTITY() AS ExpenseId
END
GO

-- Update Expense
CREATE OR ALTER PROCEDURE UpdateExpense
    @ExpenseId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL
AS
BEGIN
    UPDATE dbo.Expenses
    SET CategoryId = @CategoryId,
        AmountMinor = @AmountMinor,
        ExpenseDate = @ExpenseDate,
        Description = @Description
    WHERE ExpenseId = @ExpenseId
    AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft')
END
GO

-- Submit Expense
CREATE OR ALTER PROCEDURE SubmitExpense
    @ExpenseId INT
AS
BEGIN
    DECLARE @SubmittedStatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted')
    
    UPDATE dbo.Expenses
    SET StatusId = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
    AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft')
END
GO

-- Approve Expense
CREATE OR ALTER PROCEDURE ApproveExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    DECLARE @ApprovedStatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved')
    
    UPDATE dbo.Expenses
    SET StatusId = @ApprovedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
    AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted')
END
GO

-- Reject Expense
CREATE OR ALTER PROCEDURE RejectExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    DECLARE @RejectedStatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected')
    
    UPDATE dbo.Expenses
    SET StatusId = @RejectedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
    AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted')
END
GO

-- Get Categories
CREATE OR ALTER PROCEDURE GetCategories
AS
BEGIN
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName
END
GO

-- Get Statuses
CREATE OR ALTER PROCEDURE GetStatuses
AS
BEGIN
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId
END
GO

-- Get Users
CREATE OR ALTER PROCEDURE GetUsers
AS
BEGIN
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r ON u.RoleId = r.RoleId
    WHERE u.IsActive = 1
    ORDER BY u.UserName
END
GO
