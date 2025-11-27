using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using ExpenseManagement.Models;

namespace ExpenseManagement.Pages;

public class ApproveModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ApproveModel> _logger;

    public IEnumerable<Expense> PendingExpenses { get; set; } = Enumerable.Empty<Expense>();
    public AppError? Error { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    public ApproveModel(IExpenseService expenseService, ILogger<ApproveModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        var pending = await _expenseService.GetPendingExpensesAsync();
        
        if (!string.IsNullOrEmpty(SearchTerm))
        {
            pending = pending.Where(e => 
                (e.Description?.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.CategoryName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                e.UserName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
        }
        
        PendingExpenses = pending;
        Error = _expenseService.LastError;
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId)
    {
        var result = await _expenseService.ApproveExpenseAsync(expenseId, 2); // Manager user ID = 2
        if (result)
        {
            _logger.LogInformation("Approved expense {ExpenseId}", expenseId);
        }
        else
        {
            _logger.LogError("Failed to approve expense {ExpenseId}", expenseId);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId)
    {
        var result = await _expenseService.RejectExpenseAsync(expenseId, 2); // Manager user ID = 2
        if (result)
        {
            _logger.LogInformation("Rejected expense {ExpenseId}", expenseId);
        }
        else
        {
            _logger.LogError("Failed to reject expense {ExpenseId}", expenseId);
        }
        return RedirectToPage();
    }
}
