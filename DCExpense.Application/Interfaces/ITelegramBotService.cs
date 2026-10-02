using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCExpense.Application.DTOs;

namespace DCExpense.Application.Interfaces;

public interface ITelegramBotService
{
    Task<bool> HandleUpdateAsync(TelegramUpdateDto update, CancellationToken cancellationToken = default);
    Task<bool> SendMessageWithActionsAsync(long chatId, string text, List<(string Text, string CallbackData)> buttons);
    Task<bool> AnswerCallbackQueryAsync(string callbackQueryId, string text);
}
