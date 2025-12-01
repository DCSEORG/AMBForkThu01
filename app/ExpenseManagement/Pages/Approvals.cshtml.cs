using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly ILogger<ApprovalsModel> _logger;
    private readonly IExpenseService _expenseService;

    public List<Expense> PendingExpenses { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    public ApprovalsModel(ILogger<ApprovalsModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        var pending = await _expenseService.GetPendingApprovalsAsync();
        
        if (!string.IsNullOrEmpty(Filter))
        {
            pending = pending.Where(e =>
                (e.Description?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.CategoryName?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.UserName?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }
        
        PendingExpenses = pending;

        if (_expenseService is ExpenseService expSvc && !string.IsNullOrEmpty(expSvc.LastError))
        {
            ViewData["ErrorMessage"] = expSvc.LastError;
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId)
    {
        await _expenseService.ApproveExpenseAsync(expenseId, 2); // Manager ID = 2
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId)
    {
        await _expenseService.RejectExpenseAsync(expenseId, 2); // Manager ID = 2
        return RedirectToPage();
    }
}
