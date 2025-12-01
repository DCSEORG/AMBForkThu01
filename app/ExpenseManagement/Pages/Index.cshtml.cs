using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IExpenseService _expenseService;

    public DashboardSummary Summary { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();

    public IndexModel(ILogger<IndexModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        Summary = await _expenseService.GetDashboardSummaryAsync();
        var allExpenses = await _expenseService.GetExpensesAsync();
        RecentExpenses = allExpenses.Take(10).ToList();

        // Check for any error message from the service
        if (_expenseService is ExpenseService expSvc && !string.IsNullOrEmpty(expSvc.LastError))
        {
            ViewData["ErrorMessage"] = expSvc.LastError;
        }
    }
}
