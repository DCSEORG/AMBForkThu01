using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using ExpenseManagement.Models;
using System.ComponentModel.DataAnnotations;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<AddExpenseModel> _logger;

    public IEnumerable<Category> Categories { get; set; } = Enumerable.Empty<Category>();
    public AppError? Error { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Date is required")]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [BindProperty]
    [Required(ErrorMessage = "Category is required")]
    public int CategoryId { get; set; }

    [BindProperty]
    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    public AddExpenseModel(IExpenseService expenseService, ILogger<AddExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        Categories = await _expenseService.GetAllCategoriesAsync();
        Error = _expenseService.LastError;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Categories = await _expenseService.GetAllCategoriesAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new CreateExpenseRequest
        {
            UserId = 1, // Default user for demo
            CategoryId = CategoryId,
            Amount = Amount,
            ExpenseDate = ExpenseDate,
            Description = Description
        };

        var expenseId = await _expenseService.CreateExpenseAsync(request);
        
        if (expenseId <= 0)
        {
            Error = _expenseService.LastError;
            ModelState.AddModelError(string.Empty, "Failed to create expense. Please try again.");
            return Page();
        }

        _logger.LogInformation("Created expense {ExpenseId}", expenseId);
        return RedirectToPage("/Expenses");
    }
}
