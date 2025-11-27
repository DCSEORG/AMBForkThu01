namespace ExpenseManagement.Models;

public class UpdateExpenseRequest
{
    public int ExpenseId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
}
