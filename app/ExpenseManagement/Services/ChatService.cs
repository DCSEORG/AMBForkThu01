using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;
using System.Text.Json;

// Alias to avoid conflict with our ChatMessage model
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request);
    bool IsConfigured { get; }
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;
    private readonly GenAISettings _settings;
    private readonly AzureOpenAIClient? _client;
    private readonly ChatClient? _chatClient;

    public bool IsConfigured => _settings.IsConfigured;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;

        _settings = new GenAISettings
        {
            Endpoint = configuration["OpenAI:Endpoint"],
            DeploymentName = configuration["OpenAI:DeploymentName"],
            SearchEndpoint = configuration["OpenAI:SearchEndpoint"]
        };

        if (_settings.IsConfigured)
        {
            try
            {
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

                _client = new AzureOpenAIClient(new Uri(_settings.Endpoint!), credential);
                _chatClient = _client.GetChatClient(_settings.DeploymentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure OpenAI client");
            }
        }
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
    {
        if (!IsConfigured || _chatClient == null)
        {
            return new ChatResponse
            {
                Message = "GenAI services are not configured. To enable the AI chat assistant, please deploy the GenAI resources by running the deploy-with-chat.sh script. This will set up Azure OpenAI and AI Search services for an enhanced chat experience.",
                Success = false,
                Error = "GenAI not configured"
            };
        }

        try
        {
            var messages = new List<OpenAIChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt())
            };

            // Add conversation history
            if (request.History != null)
            {
                foreach (var msg in request.History)
                {
                    if (msg.Role == "user")
                        messages.Add(new UserChatMessage(msg.Content));
                    else if (msg.Role == "assistant")
                        messages.Add(new AssistantChatMessage(msg.Content));
                }
            }

            // Add the current message
            messages.Add(new UserChatMessage(request.Message));

            // Define function tools for the chat
            var tools = GetFunctionTools();

            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            // First API call with tools
            var response = await _chatClient.CompleteChatAsync(messages, options);
            var assistantMessage = response.Value;

            // Handle function calling loop
            while (assistantMessage.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Add assistant message with tool calls
                messages.Add(new AssistantChatMessage(assistantMessage));

                // Process each tool call
                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                    messages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                }

                // Get next response
                response = await _chatClient.CompleteChatAsync(messages, options);
                assistantMessage = response.Value;
            }

            var content = assistantMessage.Content.Count > 0 ? assistantMessage.Content[0].Text : "";

            return new ChatResponse
            {
                Message = content ?? "I couldn't generate a response. Please try again.",
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to Azure OpenAI");
            return new ChatResponse
            {
                Message = "I'm sorry, I encountered an error processing your request. Please try again.",
                Success = false,
                Error = ex.Message
            };
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are an AI assistant for the Expense Management System. You help users manage their expenses, view reports, and get information about expense policies.

You have access to the following functions to interact with the expense management system:
- get_dashboard_summary: Get a summary of all expenses including totals and pending approvals
- get_expenses: List expenses with optional filtering by search term or status
- get_pending_approvals: Get all expenses pending approval
- get_categories: Get all expense categories
- create_expense: Create a new expense (requires amount, date, category, and optional description)
- approve_expense: Approve a pending expense (requires expense ID)
- reject_expense: Reject a pending expense (requires expense ID)

When users ask about expenses, pending approvals, or want to create/approve expenses, use the appropriate functions.
Format monetary values in GBP (£) and dates in a readable format.
When listing expenses, format them in a clean, readable list with bullets or numbers.";
    }

    private static List<ChatTool> GetFunctionTools()
    {
        return new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                "get_dashboard_summary",
                "Retrieves a summary of the expense dashboard including total expenses, pending approvals, approved amounts, and approved count"
            ),
            ChatTool.CreateFunctionTool(
                "get_expenses",
                "Retrieves a list of expenses with optional filtering",
                BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        filter = new { type = "string", description = "Optional search filter for expenses" },
                        statusId = new { type = "integer", description = "Optional status ID filter (1=Draft, 2=Submitted, 3=Approved, 4=Rejected)" }
                    }
                })
            ),
            ChatTool.CreateFunctionTool(
                "get_pending_approvals",
                "Retrieves all expenses that are pending approval"
            ),
            ChatTool.CreateFunctionTool(
                "get_categories",
                "Retrieves all expense categories"
            ),
            ChatTool.CreateFunctionTool(
                "create_expense",
                "Creates a new expense",
                BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        amount = new { type = "number", description = "The expense amount in GBP" },
                        expenseDate = new { type = "string", description = "The date of the expense (ISO format)" },
                        categoryId = new { type = "integer", description = "The category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)" },
                        description = new { type = "string", description = "Description of the expense" }
                    },
                    required = new[] { "amount", "expenseDate", "categoryId" }
                })
            ),
            ChatTool.CreateFunctionTool(
                "approve_expense",
                "Approves a pending expense",
                BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        expenseId = new { type = "integer", description = "The ID of the expense to approve" }
                    },
                    required = new[] { "expenseId" }
                })
            ),
            ChatTool.CreateFunctionTool(
                "reject_expense",
                "Rejects a pending expense",
                BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        expenseId = new { type = "integer", description = "The ID of the expense to reject" }
                    },
                    required = new[] { "expenseId" }
                })
            )
        };
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            _logger.LogInformation("Executing function: {FunctionName} with arguments: {Arguments}", functionName, arguments);

            return functionName switch
            {
                "get_dashboard_summary" => await GetDashboardSummaryFunctionAsync(),
                "get_expenses" => await GetExpensesFunctionAsync(arguments),
                "get_pending_approvals" => await GetPendingApprovalsFunctionAsync(),
                "get_categories" => await GetCategoriesFunctionAsync(),
                "create_expense" => await CreateExpenseFunctionAsync(arguments),
                "approve_expense" => await ApproveExpenseFunctionAsync(arguments),
                "reject_expense" => await RejectExpenseFunctionAsync(arguments),
                _ => JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> GetDashboardSummaryFunctionAsync()
    {
        var summary = await _expenseService.GetDashboardSummaryAsync();
        return JsonSerializer.Serialize(summary);
    }

    private async Task<string> GetExpensesFunctionAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);
        string? filter = null;
        int? statusId = null;

        if (args.TryGetProperty("filter", out var filterProp))
            filter = filterProp.GetString();
        if (args.TryGetProperty("statusId", out var statusProp))
            statusId = statusProp.GetInt32();

        var expenses = await _expenseService.GetExpensesAsync(filter, statusId);
        return JsonSerializer.Serialize(expenses.Select(e => new
        {
            e.ExpenseId,
            e.UserName,
            e.CategoryName,
            Amount = $"£{e.Amount:N2}",
            e.StatusName,
            Date = e.ExpenseDate.ToString("dd MMM yyyy"),
            e.Description
        }));
    }

    private async Task<string> GetPendingApprovalsFunctionAsync()
    {
        var expenses = await _expenseService.GetPendingApprovalsAsync();
        return JsonSerializer.Serialize(expenses.Select(e => new
        {
            e.ExpenseId,
            e.UserName,
            e.CategoryName,
            Amount = $"£{e.Amount:N2}",
            Date = e.ExpenseDate.ToString("dd MMM yyyy"),
            e.Description
        }));
    }

    private async Task<string> GetCategoriesFunctionAsync()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return JsonSerializer.Serialize(categories);
    }

    private async Task<string> CreateExpenseFunctionAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);
        
        var dto = new ExpenseCreateDto
        {
            Amount = args.GetProperty("amount").GetDecimal(),
            ExpenseDate = DateTime.Parse(args.GetProperty("expenseDate").GetString()!),
            CategoryId = args.GetProperty("categoryId").GetInt32(),
            Description = args.TryGetProperty("description", out var descProp) ? descProp.GetString() : null,
            UserId = 1 // Default user
        };

        var expenseId = await _expenseService.CreateExpenseAsync(dto);
        return JsonSerializer.Serialize(new { success = true, expenseId, message = $"Expense created successfully with ID {expenseId}" });
    }

    private async Task<string> ApproveExpenseFunctionAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);
        var expenseId = args.GetProperty("expenseId").GetInt32();

        var success = await _expenseService.ApproveExpenseAsync(expenseId, 2); // Manager ID = 2
        return JsonSerializer.Serialize(new { success, message = success ? $"Expense {expenseId} approved successfully" : $"Failed to approve expense {expenseId}" });
    }

    private async Task<string> RejectExpenseFunctionAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);
        var expenseId = args.GetProperty("expenseId").GetInt32();

        var success = await _expenseService.RejectExpenseAsync(expenseId, 2); // Manager ID = 2
        return JsonSerializer.Serialize(new { success, message = success ? $"Expense {expenseId} rejected successfully" : $"Failed to reject expense {expenseId}" });
    }
}
