using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<DashboardSummary> GetDashboardSummaryAsync();
    Task<List<Expense>> GetExpensesAsync(string? filter = null, int? statusId = null);
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<List<Expense>> GetPendingApprovalsAsync();
    Task<int> CreateExpenseAsync(ExpenseCreateDto expense);
    Task<bool> UpdateExpenseAsync(ExpenseUpdateDto expense);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<List<ExpenseCategory>> GetCategoriesAsync();
    Task<List<ExpenseStatus>> GetStatusesAsync();
    Task<List<User>> GetUsersAsync();
}

public class ExpenseService : IExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;
    private readonly bool _useDummyData;
    private string? _lastError;

    public string? LastError => _lastError;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        _logger = logger;
        _useDummyData = string.IsNullOrEmpty(_connectionString);
    }

    private async Task<SqlConnection> GetConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        try
        {
            if (_useDummyData) return GetDummyDashboardSummary();

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC GetDashboardSummary", connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new DashboardSummary
                {
                    TotalExpenses = reader.GetInt32(reader.GetOrdinal("TotalExpenses")),
                    PendingApprovals = reader.GetInt32(reader.GetOrdinal("PendingApprovals")),
                    ApprovedAmount = reader.GetDecimal(reader.GetOrdinal("ApprovedAmount")),
                    ApprovedCount = reader.GetInt32(reader.GetOrdinal("ApprovedCount"))
                };
            }

            return GetDummyDashboardSummary();
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in GetDashboardSummaryAsync: {ex.Message} (File: ExpenseService.cs, Method: GetDashboardSummaryAsync)";
            _logger.LogError(ex, "Error getting dashboard summary");
            return GetDummyDashboardSummary();
        }
    }

    public async Task<List<Expense>> GetExpensesAsync(string? filter = null, int? statusId = null)
    {
        try
        {
            if (_useDummyData) return GetDummyExpenses(filter, statusId);

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC GetExpenses @Filter, @StatusId", connection);
            command.Parameters.AddWithValue("@Filter", (object?)filter ?? DBNull.Value);
            command.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);
            
            await using var reader = await command.ExecuteReaderAsync();
            var expenses = new List<Expense>();

            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return expenses;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in GetExpensesAsync: {ex.Message} (File: ExpenseService.cs, Method: GetExpensesAsync)";
            _logger.LogError(ex, "Error getting expenses");
            return GetDummyExpenses(filter, statusId);
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            if (_useDummyData) return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC GetExpenseById @ExpenseId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpense(reader);
            }

            return null;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in GetExpenseByIdAsync: {ex.Message} (File: ExpenseService.cs, Method: GetExpenseByIdAsync)";
            _logger.LogError(ex, "Error getting expense by id");
            return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);
        }
    }

    public async Task<List<Expense>> GetPendingApprovalsAsync()
    {
        try
        {
            if (_useDummyData) return GetDummyExpenses().Where(e => e.StatusName == "Submitted").ToList();

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC GetPendingApprovals", connection);
            
            await using var reader = await command.ExecuteReaderAsync();
            var expenses = new List<Expense>();

            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return expenses;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in GetPendingApprovalsAsync: {ex.Message} (File: ExpenseService.cs, Method: GetPendingApprovalsAsync)";
            _logger.LogError(ex, "Error getting pending approvals");
            return GetDummyExpenses().Where(e => e.StatusName == "Submitted").ToList();
        }
    }

    public async Task<int> CreateExpenseAsync(ExpenseCreateDto expense)
    {
        try
        {
            if (_useDummyData) return new Random().Next(100, 1000);

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC CreateExpense @UserId, @CategoryId, @AmountMinor, @ExpenseDate, @Description", connection);
            command.Parameters.AddWithValue("@UserId", expense.UserId);
            command.Parameters.AddWithValue("@CategoryId", expense.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(expense.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", expense.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)expense.Description ?? DBNull.Value);
            
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in CreateExpenseAsync: {ex.Message} (File: ExpenseService.cs, Method: CreateExpenseAsync)";
            _logger.LogError(ex, "Error creating expense");
            throw;
        }
    }

    public async Task<bool> UpdateExpenseAsync(ExpenseUpdateDto expense)
    {
        try
        {
            if (_useDummyData) return true;

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC UpdateExpense @ExpenseId, @CategoryId, @AmountMinor, @ExpenseDate, @Description", connection);
            command.Parameters.AddWithValue("@ExpenseId", expense.ExpenseId);
            command.Parameters.AddWithValue("@CategoryId", expense.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(expense.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", expense.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)expense.Description ?? DBNull.Value);
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in UpdateExpenseAsync: {ex.Message} (File: ExpenseService.cs, Method: UpdateExpenseAsync)";
            _logger.LogError(ex, "Error updating expense");
            throw;
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            if (_useDummyData) return true;

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC SubmitExpense @ExpenseId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in SubmitExpenseAsync: {ex.Message} (File: ExpenseService.cs, Method: SubmitExpenseAsync)";
            _logger.LogError(ex, "Error submitting expense");
            throw;
        }
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            if (_useDummyData) return true;

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC ApproveExpense @ExpenseId, @ReviewerId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in ApproveExpenseAsync: {ex.Message} (File: ExpenseService.cs, Method: ApproveExpenseAsync)";
            _logger.LogError(ex, "Error approving expense");
            throw;
        }
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            if (_useDummyData) return true;

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC RejectExpense @ExpenseId, @ReviewerId", connection);
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in RejectExpenseAsync: {ex.Message} (File: ExpenseService.cs, Method: RejectExpenseAsync)";
            _logger.LogError(ex, "Error rejecting expense");
            throw;
        }
    }

    public async Task<List<ExpenseCategory>> GetCategoriesAsync()
    {
        try
        {
            if (_useDummyData) return GetDummyCategories();

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC GetCategories", connection);
            
            await using var reader = await command.ExecuteReaderAsync();
            var categories = new List<ExpenseCategory>();

            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }

            return categories;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in GetCategoriesAsync: {ex.Message} (File: ExpenseService.cs, Method: GetCategoriesAsync)";
            _logger.LogError(ex, "Error getting categories");
            return GetDummyCategories();
        }
    }

    public async Task<List<ExpenseStatus>> GetStatusesAsync()
    {
        try
        {
            if (_useDummyData) return GetDummyStatuses();

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC GetStatuses", connection);
            
            await using var reader = await command.ExecuteReaderAsync();
            var statuses = new List<ExpenseStatus>();

            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }

            return statuses;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in GetStatusesAsync: {ex.Message} (File: ExpenseService.cs, Method: GetStatusesAsync)";
            _logger.LogError(ex, "Error getting statuses");
            return GetDummyStatuses();
        }
    }

    public async Task<List<User>> GetUsersAsync()
    {
        try
        {
            if (_useDummyData) return GetDummyUsers();

            await using var connection = await GetConnectionAsync();
            await using var command = new SqlCommand("EXEC GetUsers", connection);
            
            await using var reader = await command.ExecuteReaderAsync();
            var users = new List<User>();

            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.IsDBNull(reader.GetOrdinal("RoleName")) ? null : reader.GetString(reader.GetOrdinal("RoleName")),
                    ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }

            return users;
        }
        catch (Exception ex)
        {
            _lastError = $"Database error in GetUsersAsync: {ex.Message} (File: ExpenseService.cs, Method: GetUsersAsync)";
            _logger.LogError(ex, "Error getting users");
            return GetDummyUsers();
        }
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.IsDBNull(reader.GetOrdinal("StatusName")) ? null : reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    // Dummy data for when database is not available
    private static DashboardSummary GetDummyDashboardSummary() => new()
    {
        TotalExpenses = 10,
        PendingApprovals = 1,
        ApprovedAmount = 519.24m,
        ApprovedCount = 6
    };

    private static List<Expense> GetDummyExpenses(string? filter = null, int? statusId = null)
    {
        var expenses = new List<Expense>
        {
            new() { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 3, StatusName = "Approved", AmountMinor = 12300, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-10), Description = "Travel for meeting", CreatedAt = DateTime.Now.AddDays(-10) },
            new() { ExpenseId = 2, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies", StatusId = 3, StatusName = "Approved", AmountMinor = 100, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-8), Description = "111", CreatedAt = DateTime.Now.AddDays(-8) },
            new() { ExpenseId = 3, UserId = 2, UserName = "Bob Manager", CategoryId = 1, CategoryName = "Travel", StatusId = 1, StatusName = "Draft", AmountMinor = 23400, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-15), Description = "Meeting", CreatedAt = DateTime.Now.AddDays(-15) },
            new() { ExpenseId = 4, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 3, StatusName = "Approved", AmountMinor = 25000, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-20), Description = "Client dinner meeting", CreatedAt = DateTime.Now.AddDays(-20) },
            new() { ExpenseId = 5, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 1, StatusName = "Draft", AmountMinor = 25000, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-20), Description = "Client dinner meeting", CreatedAt = DateTime.Now.AddDays(-20) },
            new() { ExpenseId = 6, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 1, StatusName = "Draft", AmountMinor = 25000, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-20), Description = "Client dinner meeting", CreatedAt = DateTime.Now.AddDays(-20) },
            new() { ExpenseId = 7, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-5), Description = "Taxi from airport to client site", SubmittedAt = DateTime.Now.AddDays(-4), CreatedAt = DateTime.Now.AddDays(-5) }
        };

        if (!string.IsNullOrEmpty(filter))
        {
            expenses = expenses.Where(e => 
                (e.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.CategoryName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.UserName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        if (statusId.HasValue)
        {
            expenses = expenses.Where(e => e.StatusId == statusId.Value).ToList();
        }

        return expenses.OrderByDescending(e => e.ExpenseDate).ToList();
    }

    private static List<ExpenseCategory> GetDummyCategories() => new()
    {
        new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
        new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
        new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
    };

    private static List<ExpenseStatus> GetDummyStatuses() => new()
    {
        new() { StatusId = 1, StatusName = "Draft" },
        new() { StatusId = 2, StatusName = "Submitted" },
        new() { StatusId = 3, StatusName = "Approved" },
        new() { StatusId = 4, StatusName = "Rejected" }
    };

    private static List<User> GetDummyUsers() => new()
    {
        new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.Now.AddMonths(-6) },
        new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.Now.AddMonths(-6) }
    };
}
