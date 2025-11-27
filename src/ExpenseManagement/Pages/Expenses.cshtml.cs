using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using ExpenseManagement.Models;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesModel> _logger;

    public IEnumerable<Expense> Expenses { get; set; } = Enumerable.Empty<Expense>();
    public IEnumerable<Category> Categories { get; set; } = Enumerable.Empty<Category>();
    public IEnumerable<ExpenseStatus> Statuses { get; set; } = Enumerable.Empty<ExpenseStatus>();
    public AppError? Error { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int? CategoryFilter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int? StatusFilter { get; set; }

    public ExpensesModel(IExpenseService expenseService, ILogger<ExpensesModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        Categories = await _expenseService.GetAllCategoriesAsync();
        Statuses = await _expenseService.GetAllStatusesAsync();
        
        if (!string.IsNullOrEmpty(SearchTerm) || CategoryFilter.HasValue || StatusFilter.HasValue)
        {
            Expenses = await _expenseService.SearchExpensesAsync(SearchTerm, CategoryFilter, StatusFilter, null, null);
        }
        else
        {
            Expenses = await _expenseService.GetAllExpensesAsync();
        }
        
        Error = _expenseService.LastError;
    }

    public async Task<IActionResult> OnPostSubmitAsync(int expenseId)
    {
        var result = await _expenseService.SubmitExpenseAsync(expenseId);
        if (!result && _expenseService.LastError != null)
        {
            _logger.LogError("Failed to submit expense {ExpenseId}", expenseId);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int expenseId)
    {
        var result = await _expenseService.DeleteExpenseAsync(expenseId);
        if (!result && _expenseService.LastError != null)
        {
            _logger.LogError("Failed to delete expense {ExpenseId}", expenseId);
        }
        return RedirectToPage();
    }
}
