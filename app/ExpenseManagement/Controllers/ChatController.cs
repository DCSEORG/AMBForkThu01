using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to the AI chat assistant
    /// </summary>
    /// <param name="request">Chat request with message and optional history</param>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ChatResponse 
            { 
                Message = "Message cannot be empty", 
                Success = false 
            });
        }

        var response = await _chatService.SendMessageAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Check if the chat service is configured
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetStatus()
    {
        return Ok(new 
        { 
            configured = _chatService.IsConfigured,
            message = _chatService.IsConfigured 
                ? "GenAI services are configured and ready" 
                : "GenAI services are not configured. Run deploy-with-chat.sh to enable AI chat."
        });
    }
}
