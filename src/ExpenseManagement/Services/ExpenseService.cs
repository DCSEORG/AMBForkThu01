using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;
using System.Data;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<IEnumerable<Expense>> GetAllExpensesAsync();
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<IEnumerable<Expense>> GetExpensesByStatusAsync(string statusName);
    Task<IEnumerable<Expense>> GetPendingExpensesAsync();
    Task<IEnumerable<Expense>> GetExpensesByUserAsync(int userId);
    Task<int> CreateExpenseAsync(CreateExpenseRequest request);
    Task<bool> UpdateExpenseAsync(UpdateExpenseRequest request);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<bool> DeleteExpenseAsync(int expenseId);
    Task<IEnumerable<Category>> GetAllCategoriesAsync();
    Task<IEnumerable<ExpenseStatus>> GetAllStatusesAsync();
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User?> GetUserByIdAsync(int userId);
    Task<IEnumerable<Expense>> SearchExpensesAsync(string? searchTerm, int? categoryId, int? statusId, DateTime? startDate, DateTime? endDate);
    Task<IEnumerable<ExpenseSummary>> GetExpenseSummaryAsync();
    AppError? LastError { get; }
}

public class ExpenseService : IExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;
    public AppError? LastError { get; private set; }

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found");
        _logger = logger;
    }

    private async Task<SqlConnection> GetConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private void SetError(Exception ex, string fileName, int lineNumber)
    {
        LastError = new AppError
        {
            Message = ex.Message,
            Details = ex.InnerException?.Message,
            FileName = fileName,
            LineNumber = lineNumber,
            Timestamp = DateTime.UtcNow
        };

        // Check for managed identity specific errors
        if (ex.Message.Contains("managed identity", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("ManagedIdentity", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("AADSTS", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase))
        {
            LastError.ManagedIdentityFix = @"Managed Identity Authentication Error Fix:
1. Ensure the App Service has a User Assigned Managed Identity configured
2. Verify AZURE_CLIENT_ID is set in App Service Configuration with the Managed Identity's Client ID
3. Confirm the Managed Identity has been granted 'db_datareader', 'db_datawriter' and EXECUTE permissions on the database
4. Run 'python3 run-sql-dbrole.py' to configure the database user for the managed identity
5. For local development, use 'az login' and set connection string to use 'Authentication=Active Directory Default'";
        }

        _logger.LogError(ex, "Error in {FileName}:{LineNumber}: {Message}", fileName, lineNumber, ex.Message);
    }

    public async Task<IEnumerable<Expense>> GetAllExpensesAsync()
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetAllExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 95);
            return GetDummyExpenses();
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetExpenseById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpense(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 117);
            return GetDummyExpenses().FirstOrDefault();
        }
    }

    public async Task<IEnumerable<Expense>> GetExpensesByStatusAsync(string statusName)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetExpensesByStatus", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@StatusName", statusName);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 141);
            return GetDummyExpenses().Where(e => e.StatusName == statusName);
        }
    }

    public async Task<IEnumerable<Expense>> GetPendingExpensesAsync()
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetPendingExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 165);
            return GetDummyExpenses().Where(e => e.StatusName == "Submitted");
        }
    }

    public async Task<IEnumerable<Expense>> GetExpensesByUserAsync(int userId)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetExpensesByUser", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", userId);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 189);
            return GetDummyExpenses();
        }
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_CreateExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            
            var expenseIdParam = new SqlParameter("@ExpenseId", SqlDbType.Int) { Direction = ParameterDirection.Output };
            command.Parameters.Add(expenseIdParam);

            await command.ExecuteNonQueryAsync();
            return (int)expenseIdParam.Value;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 219);
            return -1;
        }
    }

    public async Task<bool> UpdateExpenseAsync(UpdateExpenseRequest request)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_UpdateExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 245);
            return false;
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_SubmitExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 265);
            return false;
        }
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_ApproveExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 287);
            return false;
        }
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_RejectExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 309);
            return false;
        }
    }

    public async Task<bool> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_DeleteExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 329);
            return false;
        }
    }

    public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetAllCategories", connection);
            command.CommandType = CommandType.StoredProcedure;

            var categories = new List<Category>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
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
            SetError(ex, "ExpenseService.cs", 357);
            return GetDummyCategories();
        }
    }

    public async Task<IEnumerable<ExpenseStatus>> GetAllStatusesAsync()
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetAllStatuses", connection);
            command.CommandType = CommandType.StoredProcedure;

            var statuses = new List<ExpenseStatus>();
            using var reader = await command.ExecuteReaderAsync();
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
            SetError(ex, "ExpenseService.cs", 383);
            return GetDummyStatuses();
        }
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetAllUsers", connection);
            command.CommandType = CommandType.StoredProcedure;

            var users = new List<User>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(MapUser(reader));
            }
            return users;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 407);
            return GetDummyUsers();
        }
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetUserById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapUser(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 429);
            return GetDummyUsers().FirstOrDefault();
        }
    }

    public async Task<IEnumerable<Expense>> SearchExpensesAsync(string? searchTerm, int? categoryId, int? statusId, DateTime? startDate, DateTime? endDate)
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_SearchExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@SearchTerm", (object?)searchTerm ?? DBNull.Value);
            command.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);
            command.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);
            command.Parameters.AddWithValue("@StartDate", (object?)startDate ?? DBNull.Value);
            command.Parameters.AddWithValue("@EndDate", (object?)endDate ?? DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 457);
            return GetDummyExpenses();
        }
    }

    public async Task<IEnumerable<ExpenseSummary>> GetExpenseSummaryAsync()
    {
        try
        {
            LastError = null;
            using var connection = await GetConnectionAsync();
            using var command = new SqlCommand("usp_GetExpenseSummary", connection);
            command.CommandType = CommandType.StoredProcedure;

            var summaries = new List<ExpenseSummary>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                summaries.Add(new ExpenseSummary
                {
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    ExpenseCount = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"))
                });
            }
            return summaries;
        }
        catch (Exception ex)
        {
            SetError(ex, "ExpenseService.cs", 485);
            return GetDummySummaries();
        }
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewerName")) ? null : reader.GetString(reader.GetOrdinal("ReviewerName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    private static User MapUser(SqlDataReader reader)
    {
        return new User
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
            RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
            ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
            ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? null : reader.GetString(reader.GetOrdinal("ManagerName")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    #region Dummy Data for Error Fallback

    private static IEnumerable<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new() { ExpenseId = 1, UserId = 1, UserName = "Demo User", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", Amount = 120.00m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-10), Description = "Taxi from airport (Demo Data)", CreatedAt = DateTime.Today.AddDays(-10) },
            new() { ExpenseId = 2, UserId = 1, UserName = "Demo User", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", Amount = 69.00m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-30), Description = "Client lunch meeting (Demo Data)", CreatedAt = DateTime.Today.AddDays(-30) },
            new() { ExpenseId = 3, UserId = 1, UserName = "Demo User", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", Amount = 99.50m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-5), Description = "Office supplies (Demo Data)", CreatedAt = DateTime.Today.AddDays(-5) },
            new() { ExpenseId = 4, UserId = 1, UserName = "Demo User", CategoryId = 1, CategoryName = "Transport", StatusId = 3, StatusName = "Approved", Amount = 19.20m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-60), Description = "Transport to client site (Demo Data)", CreatedAt = DateTime.Today.AddDays(-60) }
        };
    }

    private static IEnumerable<Category> GetDummyCategories()
    {
        return new List<Category>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private static IEnumerable<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };
    }

    private static IEnumerable<User> GetDummyUsers()
    {
        return new List<User>
        {
            new() { UserId = 1, UserName = "Demo Employee", Email = "demo@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.Today.AddYears(-1) },
            new() { UserId = 2, UserName = "Demo Manager", Email = "manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.Today.AddYears(-1) }
        };
    }

    private static IEnumerable<ExpenseSummary> GetDummySummaries()
    {
        return new List<ExpenseSummary>
        {
            new() { StatusName = "Draft", ExpenseCount = 1, TotalAmount = 99.50m },
            new() { StatusName = "Submitted", ExpenseCount = 1, TotalAmount = 120.00m },
            new() { StatusName = "Approved", ExpenseCount = 2, TotalAmount = 88.20m },
            new() { StatusName = "Rejected", ExpenseCount = 0, TotalAmount = 0m }
        };
    }

    #endregion
}
