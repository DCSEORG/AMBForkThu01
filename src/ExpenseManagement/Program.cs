using ExpenseManagement.Services;
using ExpenseManagement.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Expense Management API", Version = "v1" });
});

// Register services
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IChatService, ChatService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    c.RoutePrefix = "swagger";
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// API Endpoints
app.MapGet("/api/expenses", async (IExpenseService expenseService) =>
{
    var expenses = await expenseService.GetAllExpensesAsync();
    return expenseService.LastError != null 
        ? Results.Ok(new { data = expenses, error = expenseService.LastError })
        : Results.Ok(expenses);
})
.WithName("GetAllExpenses")
.WithOpenApi();

app.MapGet("/api/expenses/{id:int}", async (int id, IExpenseService expenseService) =>
{
    var expense = await expenseService.GetExpenseByIdAsync(id);
    if (expense == null) return Results.NotFound();
    return expenseService.LastError != null
        ? Results.Ok(new { data = expense, error = expenseService.LastError })
        : Results.Ok(expense);
})
.WithName("GetExpenseById")
.WithOpenApi();

app.MapGet("/api/expenses/pending", async (IExpenseService expenseService) =>
{
    var expenses = await expenseService.GetPendingExpensesAsync();
    return expenseService.LastError != null
        ? Results.Ok(new { data = expenses, error = expenseService.LastError })
        : Results.Ok(expenses);
})
.WithName("GetPendingExpenses")
.WithOpenApi();

app.MapGet("/api/expenses/status/{status}", async (string status, IExpenseService expenseService) =>
{
    var expenses = await expenseService.GetExpensesByStatusAsync(status);
    return expenseService.LastError != null
        ? Results.Ok(new { data = expenses, error = expenseService.LastError })
        : Results.Ok(expenses);
})
.WithName("GetExpensesByStatus")
.WithOpenApi();

app.MapGet("/api/expenses/user/{userId:int}", async (int userId, IExpenseService expenseService) =>
{
    var expenses = await expenseService.GetExpensesByUserAsync(userId);
    return expenseService.LastError != null
        ? Results.Ok(new { data = expenses, error = expenseService.LastError })
        : Results.Ok(expenses);
})
.WithName("GetExpensesByUser")
.WithOpenApi();

app.MapGet("/api/expenses/search", async (string? searchTerm, int? categoryId, int? statusId, DateTime? startDate, DateTime? endDate, IExpenseService expenseService) =>
{
    var expenses = await expenseService.SearchExpensesAsync(searchTerm, categoryId, statusId, startDate, endDate);
    return expenseService.LastError != null
        ? Results.Ok(new { data = expenses, error = expenseService.LastError })
        : Results.Ok(expenses);
})
.WithName("SearchExpenses")
.WithOpenApi();

app.MapPost("/api/expenses", async (CreateExpenseRequest request, IExpenseService expenseService) =>
{
    var expenseId = await expenseService.CreateExpenseAsync(request);
    if (expenseId <= 0)
        return expenseService.LastError != null
            ? Results.BadRequest(expenseService.LastError)
            : Results.BadRequest("Failed to create expense");
    return Results.Created($"/api/expenses/{expenseId}", new { expenseId });
})
.WithName("CreateExpense")
.WithOpenApi();

app.MapPut("/api/expenses/{id:int}", async (int id, UpdateExpenseRequest request, IExpenseService expenseService) =>
{
    request.ExpenseId = id;
    var result = await expenseService.UpdateExpenseAsync(request);
    if (!result)
        return expenseService.LastError != null
            ? Results.BadRequest(expenseService.LastError)
            : Results.BadRequest("Failed to update expense");
    return Results.Ok(new { success = true });
})
.WithName("UpdateExpense")
.WithOpenApi();

app.MapPost("/api/expenses/{id:int}/submit", async (int id, IExpenseService expenseService) =>
{
    var result = await expenseService.SubmitExpenseAsync(id);
    if (!result)
        return expenseService.LastError != null
            ? Results.BadRequest(expenseService.LastError)
            : Results.BadRequest("Failed to submit expense");
    return Results.Ok(new { success = true });
})
.WithName("SubmitExpense")
.WithOpenApi();

app.MapPost("/api/expenses/{id:int}/approve", async (int id, int reviewerId, IExpenseService expenseService) =>
{
    var result = await expenseService.ApproveExpenseAsync(id, reviewerId);
    if (!result)
        return expenseService.LastError != null
            ? Results.BadRequest(expenseService.LastError)
            : Results.BadRequest("Failed to approve expense");
    return Results.Ok(new { success = true });
})
.WithName("ApproveExpense")
.WithOpenApi();

app.MapPost("/api/expenses/{id:int}/reject", async (int id, int reviewerId, IExpenseService expenseService) =>
{
    var result = await expenseService.RejectExpenseAsync(id, reviewerId);
    if (!result)
        return expenseService.LastError != null
            ? Results.BadRequest(expenseService.LastError)
            : Results.BadRequest("Failed to reject expense");
    return Results.Ok(new { success = true });
})
.WithName("RejectExpense")
.WithOpenApi();

app.MapDelete("/api/expenses/{id:int}", async (int id, IExpenseService expenseService) =>
{
    var result = await expenseService.DeleteExpenseAsync(id);
    if (!result)
        return expenseService.LastError != null
            ? Results.BadRequest(expenseService.LastError)
            : Results.BadRequest("Failed to delete expense");
    return Results.Ok(new { success = true });
})
.WithName("DeleteExpense")
.WithOpenApi();

app.MapGet("/api/categories", async (IExpenseService expenseService) =>
{
    var categories = await expenseService.GetAllCategoriesAsync();
    return Results.Ok(categories);
})
.WithName("GetAllCategories")
.WithOpenApi();

app.MapGet("/api/statuses", async (IExpenseService expenseService) =>
{
    var statuses = await expenseService.GetAllStatusesAsync();
    return Results.Ok(statuses);
})
.WithName("GetAllStatuses")
.WithOpenApi();

app.MapGet("/api/users", async (IExpenseService expenseService) =>
{
    var users = await expenseService.GetAllUsersAsync();
    return Results.Ok(users);
})
.WithName("GetAllUsers")
.WithOpenApi();

app.MapGet("/api/expenses/summary", async (IExpenseService expenseService) =>
{
    var summary = await expenseService.GetExpenseSummaryAsync();
    return expenseService.LastError != null
        ? Results.Ok(new { data = summary, error = expenseService.LastError })
        : Results.Ok(summary);
})
.WithName("GetExpenseSummary")
.WithOpenApi();

// Chat API endpoint
app.MapPost("/api/chat", async (ChatRequest request, IChatService chatService) =>
{
    var response = await chatService.SendMessageAsync(request.Message);
    return Results.Ok(new { response, isConfigured = chatService.IsConfigured });
})
.WithName("SendChatMessage")
.WithOpenApi();

app.Run();

public record ChatRequest(string Message);
