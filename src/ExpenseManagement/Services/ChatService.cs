using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using ExpenseManagement.Models;
using System.Text.Json;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<string> SendMessageAsync(string message);
    bool IsConfigured { get; }
}

public class ChatService : IChatService
{
    private readonly IExpenseService _expenseService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly ChatClient? _chatClient;
    private readonly List<ChatMessage> _conversationHistory = new();

    public bool IsConfigured => _chatClient != null;

    public ChatService(IExpenseService expenseService, IConfiguration configuration, ILogger<ChatService> logger)
    {
        _expenseService = expenseService;
        _configuration = configuration;
        _logger = logger;

        var endpoint = configuration["OpenAI:Endpoint"];
        var deploymentName = configuration["OpenAI:DeploymentName"];

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(deploymentName))
        {
            try
            {
                // Use ManagedIdentityCredential with explicit client ID
                var managedIdentityClientId = configuration["ManagedIdentityClientId"];
                Azure.Core.TokenCredential credential;

                if (!string.IsNullOrEmpty(managedIdentityClientId))
                {
                    _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                    credential = new ManagedIdentityCredential(managedIdentityClientId);
                }
                else
                {
                    _logger.LogInformation("Using DefaultAzureCredential");
                    credential = new DefaultAzureCredential();
                }

                var client = new AzureOpenAIClient(new Uri(endpoint), credential);
                _chatClient = client.GetChatClient(deploymentName);
                
                _logger.LogInformation("ChatService initialized with endpoint: {Endpoint}, deployment: {Deployment}", endpoint, deploymentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ChatService");
            }
        }
        else
        {
            _logger.LogWarning("OpenAI endpoint or deployment name not configured. Chat functionality will return dummy responses.");
        }

        InitializeConversation();
    }

    private void InitializeConversation()
    {
        var systemPrompt = @"You are an intelligent assistant for the Expense Management System. You help users manage their expenses, including:
- Viewing and searching expenses
- Creating new expenses
- Submitting expenses for approval
- Approving or rejecting expenses (for managers)
- Getting expense summaries and reports

You have access to real functions that can interact with the expense database. Use these functions when users ask about their expenses or want to perform actions.

When listing expenses, format them as a clear numbered list with date, category, amount, and status.
When asked about totals or summaries, provide helpful calculations.

Always be professional, helpful, and concise in your responses.";

        _conversationHistory.Add(new SystemChatMessage(systemPrompt));
    }

    public async Task<string> SendMessageAsync(string message)
    {
        if (!IsConfigured)
        {
            return GetDummyResponse(message);
        }

        try
        {
            _conversationHistory.Add(new UserChatMessage(message));

            var tools = GetFunctionTools();
            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            var response = await _chatClient!.CompleteChatAsync(_conversationHistory, options);
            var completion = response.Value;

            // Handle function calling loop
            while (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                var assistantMessage = new AssistantChatMessage(completion);
                _conversationHistory.Add(assistantMessage);

                foreach (var toolCall in completion.ToolCalls)
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                    _conversationHistory.Add(new ToolChatMessage(toolCall.Id, functionResult));
                }

                response = await _chatClient.CompleteChatAsync(_conversationHistory, options);
                completion = response.Value;
            }

            var assistantContent = completion.Content[0].Text;
            _conversationHistory.Add(new AssistantChatMessage(assistantContent));

            return assistantContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendMessageAsync");
            return $"Sorry, I encountered an error processing your request. Error: {ex.Message}";
        }
    }

    private static IEnumerable<ChatTool> GetFunctionTools()
    {
        return new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                "get_all_expenses",
                "Retrieves all expenses from the database"
            ),
            ChatTool.CreateFunctionTool(
                "get_expense_by_id",
                "Retrieves a specific expense by its ID",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expense_id": {
                            "type": "integer",
                            "description": "The ID of the expense to retrieve"
                        }
                    },
                    "required": ["expense_id"]
                }
                """)
            ),
            ChatTool.CreateFunctionTool(
                "get_pending_expenses",
                "Retrieves all expenses with 'Submitted' status that are pending approval"
            ),
            ChatTool.CreateFunctionTool(
                "get_expenses_by_status",
                "Retrieves expenses filtered by status",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "status_name": {
                            "type": "string",
                            "description": "The status to filter by (Draft, Submitted, Approved, Rejected)"
                        }
                    },
                    "required": ["status_name"]
                }
                """)
            ),
            ChatTool.CreateFunctionTool(
                "search_expenses",
                "Searches expenses by term, category, status, or date range",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "search_term": {
                            "type": "string",
                            "description": "Text to search in description or category"
                        },
                        "category_id": {
                            "type": "integer",
                            "description": "Category ID to filter by"
                        },
                        "status_id": {
                            "type": "integer",
                            "description": "Status ID to filter by"
                        }
                    }
                }
                """)
            ),
            ChatTool.CreateFunctionTool(
                "create_expense",
                "Creates a new expense",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "user_id": {
                            "type": "integer",
                            "description": "The user ID creating the expense (default 1)"
                        },
                        "category_id": {
                            "type": "integer",
                            "description": "Category: 1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other"
                        },
                        "amount": {
                            "type": "number",
                            "description": "The expense amount in GBP"
                        },
                        "expense_date": {
                            "type": "string",
                            "description": "The date of the expense in YYYY-MM-DD format"
                        },
                        "description": {
                            "type": "string",
                            "description": "Description of the expense"
                        }
                    },
                    "required": ["category_id", "amount", "expense_date"]
                }
                """)
            ),
            ChatTool.CreateFunctionTool(
                "submit_expense",
                "Submits an expense for approval",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expense_id": {
                            "type": "integer",
                            "description": "The ID of the expense to submit"
                        }
                    },
                    "required": ["expense_id"]
                }
                """)
            ),
            ChatTool.CreateFunctionTool(
                "approve_expense",
                "Approves a submitted expense (manager action)",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expense_id": {
                            "type": "integer",
                            "description": "The ID of the expense to approve"
                        },
                        "reviewer_id": {
                            "type": "integer",
                            "description": "The manager's user ID (default 2)"
                        }
                    },
                    "required": ["expense_id"]
                }
                """)
            ),
            ChatTool.CreateFunctionTool(
                "reject_expense",
                "Rejects a submitted expense (manager action)",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expense_id": {
                            "type": "integer",
                            "description": "The ID of the expense to reject"
                        },
                        "reviewer_id": {
                            "type": "integer",
                            "description": "The manager's user ID (default 2)"
                        }
                    },
                    "required": ["expense_id"]
                }
                """)
            ),
            ChatTool.CreateFunctionTool(
                "get_expense_summary",
                "Gets a summary of expenses grouped by status with counts and totals"
            ),
            ChatTool.CreateFunctionTool(
                "get_all_categories",
                "Gets all expense categories"
            ),
            ChatTool.CreateFunctionTool(
                "get_all_users",
                "Gets all users in the system"
            )
        };
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments) ?? new();

            return functionName switch
            {
                "get_all_expenses" => JsonSerializer.Serialize(await _expenseService.GetAllExpensesAsync()),
                "get_expense_by_id" => JsonSerializer.Serialize(await _expenseService.GetExpenseByIdAsync(args["expense_id"].GetInt32())),
                "get_pending_expenses" => JsonSerializer.Serialize(await _expenseService.GetPendingExpensesAsync()),
                "get_expenses_by_status" => JsonSerializer.Serialize(await _expenseService.GetExpensesByStatusAsync(args["status_name"].GetString() ?? "Submitted")),
                "search_expenses" => JsonSerializer.Serialize(await _expenseService.SearchExpensesAsync(
                    args.TryGetValue("search_term", out var st) ? st.GetString() : null,
                    args.TryGetValue("category_id", out var ci) ? ci.GetInt32() : null,
                    args.TryGetValue("status_id", out var si) ? si.GetInt32() : null,
                    null, null)),
                "create_expense" => await CreateExpenseFromArgsAsync(args),
                "submit_expense" => await SubmitExpenseFromArgsAsync(args),
                "approve_expense" => await ApproveExpenseFromArgsAsync(args),
                "reject_expense" => await RejectExpenseFromArgsAsync(args),
                "get_expense_summary" => JsonSerializer.Serialize(await _expenseService.GetExpenseSummaryAsync()),
                "get_all_categories" => JsonSerializer.Serialize(await _expenseService.GetAllCategoriesAsync()),
                "get_all_users" => JsonSerializer.Serialize(await _expenseService.GetAllUsersAsync()),
                _ => $"Unknown function: {functionName}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return $"Error executing function: {ex.Message}";
        }
    }

    private async Task<string> CreateExpenseFromArgsAsync(Dictionary<string, JsonElement> args)
    {
        var request = new CreateExpenseRequest
        {
            UserId = args.TryGetValue("user_id", out var ui) ? ui.GetInt32() : 1,
            CategoryId = args["category_id"].GetInt32(),
            Amount = args["amount"].GetDecimal(),
            ExpenseDate = DateTime.Parse(args["expense_date"].GetString() ?? DateTime.Today.ToString("yyyy-MM-dd")),
            Description = args.TryGetValue("description", out var desc) ? desc.GetString() : null
        };

        var expenseId = await _expenseService.CreateExpenseAsync(request);
        return expenseId > 0 
            ? $"Successfully created expense with ID {expenseId}" 
            : "Failed to create expense";
    }

    private async Task<string> SubmitExpenseFromArgsAsync(Dictionary<string, JsonElement> args)
    {
        var expenseId = args["expense_id"].GetInt32();
        var result = await _expenseService.SubmitExpenseAsync(expenseId);
        return result ? $"Successfully submitted expense {expenseId}" : $"Failed to submit expense {expenseId}";
    }

    private async Task<string> ApproveExpenseFromArgsAsync(Dictionary<string, JsonElement> args)
    {
        var expenseId = args["expense_id"].GetInt32();
        var reviewerId = args.TryGetValue("reviewer_id", out var ri) ? ri.GetInt32() : 2;
        var result = await _expenseService.ApproveExpenseAsync(expenseId, reviewerId);
        return result ? $"Successfully approved expense {expenseId}" : $"Failed to approve expense {expenseId}";
    }

    private async Task<string> RejectExpenseFromArgsAsync(Dictionary<string, JsonElement> args)
    {
        var expenseId = args["expense_id"].GetInt32();
        var reviewerId = args.TryGetValue("reviewer_id", out var ri) ? ri.GetInt32() : 2;
        var result = await _expenseService.RejectExpenseAsync(expenseId, reviewerId);
        return result ? $"Successfully rejected expense {expenseId}" : $"Failed to reject expense {expenseId}";
    }

    private static string GetDummyResponse(string message)
    {
        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("expense") && (lowerMessage.Contains("list") || lowerMessage.Contains("show") || lowerMessage.Contains("all")))
        {
            return @"**Demo Mode - GenAI services not deployed**

To enable AI-powered chat, deploy the GenAI resources by running:
```
./deploy-with-chat.sh
```

Here's sample expense data:
1. 15/01/2024 - Travel - £120.00 - Submitted
2. 10/01/2023 - Meals - £69.00 - Approved
3. 04/12/2023 - Supplies - £99.50 - Approved
4. 18/12/2023 - Transport - £19.20 - Approved";
        }

        if (lowerMessage.Contains("pending") || lowerMessage.Contains("approve"))
        {
            return @"**Demo Mode - GenAI services not deployed**

Pending expenses for approval:
1. 20/01/2024 - Travel - £120.00
2. 14/12/2023 - Office Supplies - £99.50

To enable AI-powered approvals, run: `./deploy-with-chat.sh`";
        }

        if (lowerMessage.Contains("create") || lowerMessage.Contains("add") || lowerMessage.Contains("new"))
        {
            return @"**Demo Mode - GenAI services not deployed**

To create a new expense with AI assistance, deploy the GenAI resources:
```
./deploy-with-chat.sh
```

For now, use the 'Add Expense' form in the main application.";
        }

        return @"**Demo Mode - GenAI services not deployed**

I'm currently running in demo mode because Azure OpenAI has not been deployed. 
To enable full AI-powered chat functionality, run:
```
./deploy-with-chat.sh
```

This will deploy:
- Azure OpenAI with GPT-4o model
- AI Search for enhanced capabilities

In demo mode, I can only provide sample responses.";
    }
}
