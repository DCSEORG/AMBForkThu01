using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;
using ExpenseManagement.Models;
using System.ComponentModel.DataAnnotations;

namespace ExpenseManagement.Pages;

public class EditExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<EditExpenseModel> _logger;

    public IEnumerable<Category> Categories { get; set; } = Enumerable.Empty<Category>();
    public AppError? Error { get; set; }

    [BindProperty]
    public int ExpenseId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Date is required")]
    public DateTime ExpenseDate { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Category is required")]
    public int CategoryId { get; set; }

    [BindProperty]
    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    public EditExpenseModel(IExpenseService expenseService, ILogger<EditExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Categories = await _expenseService.GetAllCategoriesAsync();
        
        var expense = await _expenseService.GetExpenseByIdAsync(id);
        if (expense == null)
        {
            return NotFound();
        }

        ExpenseId = expense.ExpenseId;
        Amount = expense.Amount;
        ExpenseDate = expense.ExpenseDate;
        CategoryId = expense.CategoryId;
        Description = expense.Description;
        Error = _expenseService.LastError;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Categories = await _expenseService.GetAllCategoriesAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new UpdateExpenseRequest
        {
            ExpenseId = ExpenseId,
            CategoryId = CategoryId,
            Amount = Amount,
            ExpenseDate = ExpenseDate,
            Description = Description
        };

        var result = await _expenseService.UpdateExpenseAsync(request);
        
        if (!result)
        {
            Error = _expenseService.LastError;
            ModelState.AddModelError(string.Empty, "Failed to update expense. Please try again.");
            return Page();
        }

        _logger.LogInformation("Updated expense {ExpenseId}", ExpenseId);
        return RedirectToPage("/Expenses");
    }
}
