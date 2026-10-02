using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;

namespace DCExpense.WebApi.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly ITelegramBotService _botService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        ITelegramBotService botService,
        IConfiguration configuration,
        ILogger<TelegramWebhookController> logger)
    {
        _botService = botService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(
        [FromBody] TelegramUpdateDto update,
        [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken,
        CancellationToken cancellationToken)
    {
        var configuredSecret = _configuration["Telegram:SecretToken"];
        if (!string.IsNullOrEmpty(configuredSecret) && configuredSecret != "your-secret-token")
        {
            if (string.IsNullOrEmpty(secretToken) || !string.Equals(secretToken, configuredSecret, StringComparison.Ordinal))
            {
                _logger.LogWarning("Unauthorized Telegram webhook call. Secret token mismatch.");
                return Unauthorized(new { error = "Invalid secret token." });
            }
        }

        try
        {
            var result = await _botService.HandleUpdateAsync(update, cancellationToken);
            return Ok(new { success = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telegram update {UpdateId}", update?.UpdateId);
            return StatusCode(500, new { error = "Internal server error while processing update." });
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var botToken = _configuration["Telegram:BotToken"];
        var isConfigured = !string.IsNullOrEmpty(botToken) && !botToken.StartsWith("YOUR_");

        return Ok(new
        {
            status = "active",
            isConfigured = isConfigured,
            timestamp = DateTime.UtcNow
        });
    }
}
