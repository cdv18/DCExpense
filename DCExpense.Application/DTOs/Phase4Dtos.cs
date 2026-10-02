using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DCExpense.Domain.Enums;

namespace DCExpense.Application.DTOs;

#region Telegram DTOs

public class TelegramUpdateDto
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessageDto? Message { get; set; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQueryDto? CallbackQuery { get; set; }
}

public class TelegramMessageDto
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    [JsonPropertyName("from")]
    public TelegramUserDto? From { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChatDto Chat { get; set; } = null!;

    [JsonPropertyName("date")]
    public long? Date { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("voice")]
    public TelegramVoiceDto? Voice { get; set; }
}

public class TelegramVoiceDto
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;

    [JsonPropertyName("file_unique_id")]
    public string? FileUniqueId { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; set; }
}

public class TelegramUserDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("language_code")]
    public string? LanguageCode { get; set; }
}

public class TelegramChatDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
}

public class TelegramCallbackQueryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public TelegramUserDto From { get; set; } = null!;

    [JsonPropertyName("message")]
    public TelegramMessageDto? Message { get; set; }

    [JsonPropertyName("inline_message_id")]
    public string? InlineMessageId { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

#endregion

#region NLP Parser DTO

public class ExpenseParsedResultDto
{
    public decimal Amount { get; set; }
    public string? CategoryName { get; set; }
    public string? AccountName { get; set; }
    public string? Note { get; set; }
    public DateTime? TransactionDate { get; set; }
    public TransactionType Type { get; set; } = TransactionType.Expense;
    public double ConfidenceScore { get; set; }
    public bool IsSuccess { get; set; }
}

#endregion

#region Household DTOs

public record CreateHouseholdDto(string Name);

public record HouseholdMemberDto(Guid Id, Guid HouseholdId, string UserId, HouseholdRole Role);

public class HouseholdDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? InviteCode { get; set; }
    public DateTime? InviteCodeExpiresAt { get; set; }
    public List<HouseholdMemberDto> Members { get; set; } = new();
    public List<AccountDto> Accounts { get; set; } = new();
}

public record JoinHouseholdDto(string InviteCode);

public record UpdateMemberRoleDto(HouseholdRole NewRole);

public record GenerateInviteCodeResultDto(string InviteCode, DateTime ExpiresAt);

#endregion
