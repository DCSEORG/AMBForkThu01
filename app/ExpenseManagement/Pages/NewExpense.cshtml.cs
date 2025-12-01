using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class NewExpenseModel : PageModel
{
    private readonly ILogger<NewExpenseModel> _logger;
    private readonly IExpenseService _expenseService;

    [BindProperty]
    public ExpenseCreateDto Expense { get; set; } = new() { ExpenseDate = DateTime.Today, CategoryId = 1 };
    
    public List<ExpenseCategory> Categories { get; set; } = new();

    public NewExpenseModel(ILogger<NewExpenseModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        Categories = await _expenseService.GetCategoriesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Categories = await _expenseService.GetCategoriesAsync();
        
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _expenseService.CreateExpenseAsync(Expense);
            return RedirectToPage("/Expenses");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            ViewData["ErrorMessage"] = ex.Message;
            return Page();
        }
    }
}
