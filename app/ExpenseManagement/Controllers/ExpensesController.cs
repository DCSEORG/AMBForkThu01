using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get dashboard summary with expense statistics
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardSummary), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummary>> GetDashboardSummary()
    {
        var summary = await _expenseService.GetDashboardSummaryAsync();
        return Ok(summary);
    }

    /// <summary>
    /// Get all expenses with optional filtering
    /// </summary>
    /// <param name="filter">Optional search filter</param>
    /// <param name="statusId">Optional status filter (1=Draft, 2=Submitted, 3=Approved, 4=Rejected)</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Expense>>> GetExpenses([FromQuery] string? filter = null, [FromQuery] int? statusId = null)
    {
        var expenses = await _expenseService.GetExpensesAsync(filter, statusId);
        return Ok(expenses);
    }

    /// <summary>
    /// Get a specific expense by ID
    /// </summary>
    /// <param name="id">Expense ID</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Expense>> GetExpense(int id)
    {
        var expense = await _expenseService.GetExpenseByIdAsync(id);
        if (expense == null)
        {
            return NotFound();
        }
        return Ok(expense);
    }

    /// <summary>
    /// Get all expenses pending approval
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Expense>>> GetPendingApprovals()
    {
        var expenses = await _expenseService.GetPendingApprovalsAsync();
        return Ok(expenses);
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    /// <param name="expense">Expense details</param>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateExpense([FromBody] ExpenseCreateDto expense)
    {
        try
        {
            var id = await _expenseService.CreateExpenseAsync(expense);
            return CreatedAtAction(nameof(GetExpense), new { id }, new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="expense">Updated expense details</param>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateExpense(int id, [FromBody] ExpenseUpdateDto expense)
    {
        if (id != expense.ExpenseId)
        {
            return BadRequest(new { error = "ID mismatch" });
        }

        try
        {
            var success = await _expenseService.UpdateExpenseAsync(expense);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    /// <param name="id">Expense ID</param>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SubmitExpense(int id)
    {
        try
        {
            var success = await _expenseService.SubmitExpenseAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="reviewerId">Reviewer's user ID</param>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ApproveExpense(int id, [FromQuery] int reviewerId = 2)
    {
        try
        {
            var success = await _expenseService.ApproveExpenseAsync(id, reviewerId);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    /// <param name="id">Expense ID</param>
    /// <param name="reviewerId">Reviewer's user ID</param>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RejectExpense(int id, [FromQuery] int reviewerId = 2)
    {
        try
        {
            var success = await _expenseService.RejectExpenseAsync(id, reviewerId);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<ExpenseCategory>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExpenseCategory>>> GetCategories()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return Ok(categories);
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    [HttpGet("statuses")]
    [ProducesResponseType(typeof(List<ExpenseStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExpenseStatus>>> GetStatuses()
    {
        var statuses = await _expenseService.GetStatusesAsync();
        return Ok(statuses);
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(List<User>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<User>>> GetUsers()
    {
        var users = await _expenseService.GetUsersAsync();
        return Ok(users);
    }
}
