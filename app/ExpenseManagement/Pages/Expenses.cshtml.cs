using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly ILogger<ExpensesModel> _logger;
    private readonly IExpenseService _expenseService;

    public List<Expense> Expenses { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int? StatusId { get; set; }

    public ExpensesModel(ILogger<ExpensesModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        Expenses = await _expenseService.GetExpensesAsync(Filter, StatusId);
        Statuses = await _expenseService.GetStatusesAsync();

        if (_expenseService is ExpenseService expSvc && !string.IsNullOrEmpty(expSvc.LastError))
        {
            ViewData["ErrorMessage"] = expSvc.LastError;
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int expenseId)
    {
        await _expenseService.SubmitExpenseAsync(expenseId);
        return RedirectToPage();
    }
}
