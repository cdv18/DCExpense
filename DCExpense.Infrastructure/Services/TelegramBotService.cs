using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DCExpense.Infrastructure.Services;

public class TelegramBotService : ITelegramBotService
{
    private readonly IApplicationDbContext _context;
    private readonly IAiVoiceExpenseParser _aiParser;
    private readonly ITransactionStateMachine _stateMachine;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramBotService>? _logger;

    public TelegramBotService(
        IApplicationDbContext context,
        IAiVoiceExpenseParser aiParser,
        ITransactionStateMachine stateMachine,
        IConfiguration configuration,
        HttpClient? httpClient = null,
        ILogger<TelegramBotService>? logger = null)
    {
        _context = context;
        _aiParser = aiParser;
        _stateMachine = stateMachine;
        _configuration = configuration;
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger;
    }

    public async Task<bool> HandleUpdateAsync(TelegramUpdateDto update, CancellationToken cancellationToken = default)
    {
        if (update == null) return false;

        // 1. Handle Callback Query
        if (update.CallbackQuery != null)
        {
            return await HandleCallbackQueryAsync(update.CallbackQuery, cancellationToken);
        }

        // 2. Handle Message
        if (update.Message != null)
        {
            return await HandleMessageAsync(update.Message, cancellationToken);
        }

        return false;
    }

    private async Task<bool> HandleMessageAsync(TelegramMessageDto message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var text = message.Text?.Trim();

        // 1. Check command /start
        if (text != null && text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            var welcomeText = "👋 Chào mừng bạn đến với **DCExpense Bot**!\n\n" +
                              "Tôi hỗ trợ ghi chép thu chi thông minh bằng tin nhắn văn bản và giọng nói.\n\n" +
                              "📌 Các lệnh hỗ trợ:\n" +
                              "• `/help` - Xem hướng dẫn chi tiết\n" +
                              "• `/link <mã_mời>` - Liên kết với gia đình qua mã mời\n\n" +
                              "💡 Hãy thử gõ: `Ăn trưa phở bò 45k momo` hoặc gửi tin nhắn thoại!";
            return await SendMessageWithActionsAsync(chatId, welcomeText, new List<(string, string)>());
        }

        // 2. Check command /help
        if (text != null && text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
        {
            var helpText = "💡 **Hướng dẫn sử dụng DCExpense Bot**:\n\n" +
                           "1. **Ghi chi tiêu bằng văn bản**:\n" +
                           "   - `Ăn phở 50k tiền mặt`\n" +
                           "   - `Đổ xăng 75k vcb hôm nay`\n" +
                           "   - `Mua rau chợ 2 lít momo`\n" +
                           "   - `1.2 củ mua vé máy bay techcombank`\n\n" +
                           "2. **Gửi tin nhắn thoại**:\n" +
                           "   - Nhấn giữ biểu tượng micro và nói câu chi tiêu bất kỳ.\n\n" +
                           "3. **Liên kết với nhóm gia đình**:\n" +
                           "   - Gõ `/link <MÃ_MỜI_6_KÝ_TỰ>` (lấy mã mời từ mục Quản lý gia đình).";
            return await SendMessageWithActionsAsync(chatId, helpText, new List<(string, string)>());
        }

        // 3. Check command /link <code>
        if (text != null && text.StartsWith("/link", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                return await SendMessageWithActionsAsync(chatId, "⚠️ Vui lòng cung cấp mã mời hợp lệ. Cú pháp: `/link <mã_mời>`", new List<(string, string)>());
            }

            var inviteCode = parts[1].ToUpperInvariant();
            var household = await _context.Households
                .Include(h => h.Members)
                .FirstOrDefaultAsync(h => h.InviteCode == inviteCode, cancellationToken);

            if (household == null || !household.InviteCodeExpiresAt.HasValue || household.InviteCodeExpiresAt.Value < DateTime.UtcNow)
            {
                return await SendMessageWithActionsAsync(chatId, "❌ Mã mời không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu chủ gia đình tạo mã mới.", new List<(string, string)>());
            }

            var tgUserId = message.From != null ? $"tg_{message.From.Id}" : $"tg_{chatId}";
            var existingMember = household.Members.FirstOrDefault(m => m.UserId == tgUserId);
            if (existingMember == null)
            {
                var newMember = new HouseholdMember
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = household.Id,
                    UserId = tgUserId,
                    Role = HouseholdRole.Member
                };
                _context.HouseholdMembers.Add(newMember);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return await SendMessageWithActionsAsync(chatId, $"🎉 Liên kết thành công! Bạn đã tham gia vào gia đình: **{household.Name}**.", new List<(string, string)>());
        }

        // 4. Voice Message
        if (message.Voice != null)
        {
            return await HandleVoiceAsync(chatId, message, cancellationToken);
        }

        // 5. Normal text expense parsing
        if (!string.IsNullOrWhiteSpace(text))
        {
            return await HandleExpenseTextAsync(chatId, message, text, cancellationToken);
        }

        return false;
    }

    private async Task<bool> HandleVoiceAsync(long chatId, TelegramMessageDto message, CancellationToken cancellationToken)
    {
        Stream voiceStream = Stream.Null;

        // Try downloading voice stream if bot token is provided
        var token = _configuration["Telegram:BotToken"];
        if (!string.IsNullOrEmpty(token) && !token.StartsWith("YOUR_") && !string.IsNullOrEmpty(message.Voice?.FileId))
        {
            try
            {
                var fileInfoRes = await _httpClient.GetFromJsonAsync<JsonDocument>($"https://api.telegram.org/bot{token}/getFile?file_id={message.Voice.FileId}", cancellationToken);
                var filePath = fileInfoRes?.RootElement.GetProperty("result").GetProperty("file_path").GetString();
                if (!string.IsNullOrEmpty(filePath))
                {
                    var fileBytes = await _httpClient.GetByteArrayAsync($"https://api.telegram.org/file/bot{token}/{filePath}", cancellationToken);
                    voiceStream = new MemoryStream(fileBytes);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to download voice file from Telegram API, using fallback.");
            }
        }

        var parsed = await _aiParser.ParseVoiceAudioAsync(voiceStream, "voice.ogg", cancellationToken);
        if (!parsed.IsSuccess)
        {
            return await SendMessageWithActionsAsync(chatId, "⚠️ Không nhận diện được chi tiêu từ tin nhắn thoại. Vui lòng nói rõ số tiền (ví dụ: 'Cà phê 30k tiền mặt').", new List<(string, string)>());
        }

        return await CreateDraftTransactionAndReplyAsync(chatId, message, parsed, "🎤 [Ghi âm giọng nói]", cancellationToken);
    }

    private async Task<bool> HandleExpenseTextAsync(long chatId, TelegramMessageDto message, string rawText, CancellationToken cancellationToken)
    {
        var parsed = _aiParser.ParseExpenseText(rawText);
        if (!parsed.IsSuccess)
        {
            return await SendMessageWithActionsAsync(chatId, "⚠️ Không nhận diện được số tiền trong tin nhắn. Vui lòng nhập theo cú pháp: 'Cà phê 35k tiền mặt' hoặc 'Đổ xăng 50k vcb'.", new List<(string, string)>());
        }

        return await CreateDraftTransactionAndReplyAsync(chatId, message, parsed, rawText, cancellationToken);
    }

    private async Task<bool> CreateDraftTransactionAndReplyAsync(long chatId, TelegramMessageDto message, ExpenseParsedResultDto parsed, string rawInput, CancellationToken cancellationToken)
    {
        var tgUserId = message.From != null ? $"tg_{message.From.Id}" : $"tg_{chatId}";

        // Find or associate household
        var household = await _context.Households
            .Include(h => h.Accounts)
            .Include(h => h.Categories)
            .Include(h => h.Members)
            .FirstOrDefaultAsync(h => h.Members.Any(m => m.UserId == tgUserId), cancellationToken);

        if (household == null)
        {
            // Fallback to the first existing household or create a default one
            household = await _context.Households
                .Include(h => h.Accounts)
                .Include(h => h.Categories)
                .Include(h => h.Members)
                .FirstOrDefaultAsync(cancellationToken);

            if (household == null)
            {
                household = new Household
                {
                    Id = Guid.NewGuid(),
                    Name = "Gia đình của tôi",
                    CreatedAt = DateTime.UtcNow
                };
                household.Members.Add(new HouseholdMember
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = household.Id,
                    UserId = tgUserId,
                    Role = HouseholdRole.Owner
                });
                household.Accounts.Add(new Account
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = household.Id,
                    Name = "Ví tiền mặt",
                    Type = AccountType.Cash,
                    Currency = "VND",
                    IsShared = true
                });
                _context.Households.Add(household);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // Match or select Account
        Account? targetAccount = null;
        if (!string.IsNullOrEmpty(parsed.AccountName))
        {
            targetAccount = household.Accounts.FirstOrDefault(a => a.Name.Contains(parsed.AccountName, StringComparison.OrdinalIgnoreCase));
        }

        if (targetAccount == null)
        {
            // Pick shared wallet or first available account
            targetAccount = household.Accounts.FirstOrDefault(a => a.IsShared) ?? household.Accounts.FirstOrDefault();
            if (targetAccount == null)
            {
                targetAccount = new Account
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = household.Id,
                    Name = parsed.AccountName ?? "Ví chính",
                    Type = AccountType.Cash,
                    Currency = "VND",
                    IsShared = true
                };
                _context.Accounts.Add(targetAccount);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // Match or select Category
        Category? targetCategory = null;
        if (!string.IsNullOrEmpty(parsed.CategoryName))
        {
            targetCategory = household.Categories.FirstOrDefault(c => c.Name.Equals(parsed.CategoryName, StringComparison.OrdinalIgnoreCase));
            if (targetCategory == null)
            {
                targetCategory = new Category
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = household.Id,
                    Name = parsed.CategoryName,
                    DefaultBudgetType = BudgetType.Need
                };
                _context.Categories.Add(targetCategory);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        var txDate = parsed.TransactionDate ?? DateTime.UtcNow;
        var note = !string.IsNullOrWhiteSpace(parsed.Note) ? parsed.Note : rawInput;
        var fingerprint = FingerprintHelper.Generate($"{parsed.Amount}_{txDate:yyyyMMdd}_{note}_{tgUserId}");

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = targetAccount.Id,
            CategoryId = targetCategory?.Id,
            Amount = parsed.Amount,
            Date = txDate,
            Note = note,
            Type = parsed.Type,
            Status = TransactionStatus.Draft, // Created as Draft
            CreatedByUserId = tgUserId,
            Fingerprint = fingerprint
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        var buttons = new List<(string Text, string CallbackData)>
        {
            ("✅ Xác nhận", $"tx_confirm_{transaction.Id}"),
            ("❌ Hủy", $"tx_cancel_{transaction.Id}")
        };

        var typeStr = parsed.Type == TransactionType.Income ? "Thu nhập" : "Chi tiêu";
        var replyText = $"📝 **Đã tạo giao dịch nháp (Draft)**:\n\n" +
                        $"• Loại: {typeStr}\n" +
                        $"• Số tiền: **{parsed.Amount:N0} đ**\n" +
                        $"• Danh mục: {targetCategory?.Name ?? "Chung"}\n" +
                        $"• Tài khoản/Ví: {targetAccount.Name}\n" +
                        $"• Ngày: {txDate:dd/MM/yyyy}\n" +
                        $"• Ghi chú: {note}\n" +
                        $"• Độ tin cậy AI: {parsed.ConfidenceScore * 100:0}%\n\n" +
                        "Vui lòng nhấn **Xác nhận** để ghi vào sổ chi tiêu, hoặc **Hủy** để xóa.";

        return await SendMessageWithActionsAsync(chatId, replyText, buttons);
    }

    private async Task<bool> HandleCallbackQueryAsync(TelegramCallbackQueryDto callbackQuery, CancellationToken cancellationToken)
    {
        var data = callbackQuery.Data;
        if (string.IsNullOrEmpty(data)) return false;

        if (data.StartsWith("tx_confirm_"))
        {
            var idStr = data["tx_confirm_".Length..];
            if (Guid.TryParse(idStr, out var txId))
            {
                var tx = await _context.Transactions.FindAsync(new object[] { txId }, cancellationToken);
                if (tx != null)
                {
                    if (tx.Status == TransactionStatus.Draft)
                    {
                        _stateMachine.ChangeState(tx, TransactionStatus.Pending);
                        await _context.SaveChangesAsync(cancellationToken);
                        await AnswerCallbackQueryAsync(callbackQuery.Id, "✅ Đã xác nhận thành công!");

                        if (callbackQuery.Message != null)
                        {
                            await SendMessageWithActionsAsync(callbackQuery.Message.Chat.Id, "✅ Giao dịch đã được duyệt vào sổ chi tiêu (Trạng thái: Pending).", new List<(string, string)>());
                        }
                        return true;
                    }

                    await AnswerCallbackQueryAsync(callbackQuery.Id, $"Giao dịch đang ở trạng thái {tx.Status}.");
                    return true;
                }
            }

            await AnswerCallbackQueryAsync(callbackQuery.Id, "Giao dịch không tồn tại hoặc đã bị xóa.");
            return false;
        }

        if (data.StartsWith("tx_cancel_"))
        {
            var idStr = data["tx_cancel_".Length..];
            if (Guid.TryParse(idStr, out var txId))
            {
                var tx = await _context.Transactions.FindAsync(new object[] { txId }, cancellationToken);
                if (tx != null)
                {
                    _context.Transactions.Remove(tx);
                    await _context.SaveChangesAsync(cancellationToken);
                    await AnswerCallbackQueryAsync(callbackQuery.Id, "❌ Đã hủy giao dịch.");

                    if (callbackQuery.Message != null)
                    {
                        await SendMessageWithActionsAsync(callbackQuery.Message.Chat.Id, "❌ Giao dịch nháp đã được hủy thành công.", new List<(string, string)>());
                    }
                    return true;
                }
            }

            await AnswerCallbackQueryAsync(callbackQuery.Id, "Giao dịch không tồn tại.");
            return false;
        }

        return false;
    }

    public async Task<bool> SendMessageWithActionsAsync(long chatId, string text, List<(string Text, string CallbackData)> buttons)
    {
        var token = _configuration["Telegram:BotToken"];
        if (string.IsNullOrEmpty(token) || token.StartsWith("YOUR_"))
        {
            _logger?.LogInformation("[TelegramMock] To ChatId={ChatId}: {Text} | Buttons={ButtonsCount}", chatId, text, buttons.Count);
            return true;
        }

        try
        {
            var payload = new Dictionary<string, object>
            {
                ["chat_id"] = chatId,
                ["text"] = text,
                ["parse_mode"] = "Markdown"
            };

            if (buttons.Count > 0)
            {
                var inlineKeyboard = new List<List<object>>();
                var row = new List<object>();
                foreach (var (btnText, callbackData) in buttons)
                {
                    row.Add(new { text = btnText, callback_data = callbackData });
                }
                inlineKeyboard.Add(row);
                payload["reply_markup"] = new { inline_keyboard = inlineKeyboard };
            }

            var response = await _httpClient.PostAsJsonAsync($"https://api.telegram.org/bot{token}/sendMessage", payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send Telegram message to chat {ChatId}", chatId);
            return false;
        }
    }

    public async Task<bool> AnswerCallbackQueryAsync(string callbackQueryId, string text)
    {
        var token = _configuration["Telegram:BotToken"];
        if (string.IsNullOrEmpty(token) || token.StartsWith("YOUR_"))
        {
            _logger?.LogInformation("[TelegramMock] AnswerCallbackQuery Id={QueryId}: {Text}", callbackQueryId, text);
            return true;
        }

        try
        {
            var payload = new
            {
                callback_query_id = callbackQueryId,
                text = text
            };

            var response = await _httpClient.PostAsJsonAsync($"https://api.telegram.org/bot{token}/answerCallbackQuery", payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to answer Telegram callback query {QueryId}", callbackQueryId);
            return false;
        }
    }
}
