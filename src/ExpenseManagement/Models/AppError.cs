namespace ExpenseManagement.Models;

/// <summary>
/// Represents application error information to be displayed in the UI
/// </summary>
public class AppError
{
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? FileName { get; set; }
    public int? LineNumber { get; set; }
    public string? ManagedIdentityFix { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public bool IsManagedIdentityError => !string.IsNullOrEmpty(ManagedIdentityFix);
}
