using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using ExpenseManagement.Models;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IExpenseService _expenseService;

    public IEnumerable<Expense> RecentExpenses { get; set; } = Enumerable.Empty<Expense>();
    public IEnumerable<ExpenseSummary> ExpenseSummaries { get; set; } = Enumerable.Empty<ExpenseSummary>();
    public AppError? Error { get; set; }

    public IndexModel(ILogger<IndexModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        var allExpenses = await _expenseService.GetAllExpensesAsync();
        RecentExpenses = allExpenses.Take(5);
        ExpenseSummaries = await _expenseService.GetExpenseSummaryAsync();
        Error = _expenseService.LastError;
    }
}
