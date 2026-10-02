using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Enums;

namespace DCExpense.Application.Services;

public class AiVoiceExpenseParser : IAiVoiceExpenseParser
{
    private static readonly Dictionary<string, string[]> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ăn uống"] = new[]
        {
            "phở", "cơm", "cafe", "cà phê", "nhậu", "bún", "trà sữa", "bánh mì", "lẩu",
            "ăn trưa", "ăn tối", "ăn sáng", "bò kho", "hủ tiếu", "pizza", "ăn vặt",
            "đồ ăn", "nước ngọt", "bia", "mì cay", "gà rán", "kfc", "lotteria", "highland", "phúc long"
        },
        ["Đi lại"] = new[]
        {
            "xăng", "grab", "taxi", "gửi xe", "xe buýt", "be", "gojek", "vé xe", "vé tàu",
            "vé máy bay", "cầu đường", "đỗ xe", "sửa xe", "rửa xe", "bảo dưỡng"
        },
        ["Tiện ích"] = new[]
        {
            "điện", "nước", "internet", "wifi", "tiền mạng", "truyền hình", "hóa đơn",
            "điện thoại", "nạp thẻ", "rác", "phí dịch vụ", "tiền nhà"
        },
        ["Siêu thị"] = new[]
        {
            "chợ", "bách hóa", "siêu thị", "winmart", "coopmart", "bhx", "bách hóa xanh",
            "tạp hóa", "mua rau", "thịt", "cá", "mua đồ ăn"
        },
        ["Mua sắm"] = new[]
        {
            "mua sắm", "mua sam", "sắm", "shopee", "lazada", "tiki", "quần áo", "giày", "dép", "túi xách", "mỹ phẩm", "sắm đồ"
        },
        ["Lương & Thu nhập"] = new[]
        {
            "lương", "thưởng", "hoàn tiền", "nhận tiền", "bán đồ", "thu nợ", "lãi", "cổ tức", "tiền về"
        },
        ["Giải trí"] = new[]
        {
            "xem phim", "cgv", "netflix", "spotify", "karaoke", "du lịch", "vé xem", "game"
        },
        ["Y tế"] = new[]
        {
            "thuốc", "bệnh viện", "khám bệnh", "nha khoa", "mua thuốc", "bác sĩ"
        }
    };

    private static readonly Dictionary<string, string[]> AccountKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tiền mặt"] = new[] { "tiền mặt", "tien mat", "cash" },
        ["Vietcombank"] = new[] { "vietcombank", "vcb" },
        ["Techcombank"] = new[] { "techcombank", "tech", "tcb" },
        ["Momo"] = new[] { "ví momo", "vi momo", "momo" },
        ["ZaloPay"] = new[] { "ví zalopay", "vi zalo", "zalo pay", "zalopay" },
        ["Thẻ tín dụng / Visa"] = new[] { "visa", "thẻ tín dụng", "the tin dung", "mastercard", "credit" },
        ["MBBank"] = new[] { "mbbank", "mb bank", "mb", "quân đội" }
    };

    public ExpenseParsedResultDto ParseExpenseText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ExpenseParsedResultDto
            {
                IsSuccess = false,
                ConfidenceScore = 0
            };
        }

        var normalized = text.Trim();
        var (amount, amountMatchSpan) = ExtractAmount(normalized);
        var (category, categoryMatchSpan) = ExtractCategory(normalized);
        var (account, accountMatchSpan) = ExtractAccount(normalized);
        var (date, dateMatchSpan) = ExtractDate(normalized);
        var type = DetermineTransactionType(normalized, category);
        var note = ExtractNote(normalized, new[] { amountMatchSpan, accountMatchSpan, dateMatchSpan });

        double score = 0;
        if (amount > 0) score += 0.45;
        if (!string.IsNullOrEmpty(category)) score += 0.25;
        if (!string.IsNullOrEmpty(account)) score += 0.15;
        if (date.HasValue) score += 0.10;
        if (!string.IsNullOrEmpty(note)) score += 0.05;

        bool isSuccess = amount > 0;

        return new ExpenseParsedResultDto
        {
            Amount = amount,
            CategoryName = category,
            AccountName = account,
            Note = string.IsNullOrWhiteSpace(note) ? normalized : note,
            TransactionDate = date ?? DateTime.Today,
            Type = type,
            ConfidenceScore = Math.Round(score, 2),
            IsSuccess = isSuccess
        };
    }

    public async Task<ExpenseParsedResultDto> ParseVoiceAudioAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (audioStream == null)
        {
            return new ExpenseParsedResultDto { IsSuccess = false, ConfidenceScore = 0 };
        }

        string transcribedText = string.Empty;

        // Try reading content if it's text-based (e.g. testing stub / mock transcript)
        try
        {
            if (audioStream.CanSeek)
            {
                audioStream.Position = 0;
            }

            using var reader = new StreamReader(audioStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var content = await reader.ReadToEndAsync(cancellationToken);

            // Check if content looks like valid readable text (contains letters or digits, not pure binary)
            if (!string.IsNullOrWhiteSpace(content) && content.Any(char.IsLetter) && !content.Take(20).Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
            {
                transcribedText = content.Trim();
            }
        }
        catch
        {
            // Ignore stream read errors and proceed to fallback
        }

        // If not plain text, check fileName for hints or fallback mock STT
        if (string.IsNullOrWhiteSpace(transcribedText))
        {
            if (!string.IsNullOrEmpty(fileName) && fileName.Contains("_"))
            {
                var cleanName = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ');
                if (cleanName.Any(char.IsDigit) || cleanName.Any(char.IsLetter))
                {
                    transcribedText = cleanName;
                }
            }

            if (string.IsNullOrWhiteSpace(transcribedText))
            {
                // Default STT stub
                transcribedText = "Cà phê 50k tiền mặt";
            }
        }

        return ParseExpenseText(transcribedText);
    }

    private static (decimal Amount, (int Start, int Length)? MatchSpan) ExtractAmount(string text)
    {
        // 1. Pattern: "1tr5", "1tr500", "2tr", "2.5tr"
        var matchTr = Regex.Match(text, @"(?i)\b(\d+(?:[.,]\d+)?)\s*tr\s*(\d+)?\b");
        if (matchTr.Success)
        {
            var wholePartStr = matchTr.Groups[1].Value.Replace(',', '.');
            if (decimal.TryParse(wholePartStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var baseVal))
            {
                decimal amount;
                if (matchTr.Groups[2].Success && !string.IsNullOrEmpty(matchTr.Groups[2].Value))
                {
                    var subPartStr = matchTr.Groups[2].Value;
                    // e.g. "1tr5" -> 1.5 million. "1tr50" -> 1.5 million or 1.05 million depending on standard, typically subPart represents tenth or fraction
                    if (decimal.TryParse(subPartStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var subVal))
                    {
                        var divisor = Math.Pow(10, subPartStr.Length);
                        amount = (baseVal * 1_000_000m) + (decimal)(subVal / (decimal)divisor * 1_000_000m);
                    }
                    else
                    {
                        amount = baseVal * 1_000_000m;
                    }
                }
                else
                {
                    amount = baseVal * 1_000_000m;
                }

                return (amount, (matchTr.Index, matchTr.Length));
            }
        }

        // 2. Pattern: "1.2 củ", "2 củ", "1củ5"
        var matchCu = Regex.Match(text, @"(?i)\b(\d+(?:[.,]\d+)?)\s*củ\s*(\d+)?\b");
        if (matchCu.Success)
        {
            var wholePartStr = matchCu.Groups[1].Value.Replace(',', '.');
            if (decimal.TryParse(wholePartStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var baseVal))
            {
                decimal amount;
                if (matchCu.Groups[2].Success && !string.IsNullOrEmpty(matchCu.Groups[2].Value))
                {
                    var subPartStr = matchCu.Groups[2].Value;
                    if (decimal.TryParse(subPartStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var subVal))
                    {
                        var divisor = Math.Pow(10, subPartStr.Length);
                        amount = (baseVal * 1_000_000m) + (decimal)(subVal / (decimal)divisor * 1_000_000m);
                    }
                    else
                    {
                        amount = baseVal * 1_000_000m;
                    }
                }
                else
                {
                    amount = baseVal * 1_000_000m;
                }

                return (amount, (matchCu.Index, matchCu.Length));
            }
        }

        // 3. Pattern: "triệu" / "trieu", e.g. "1.5 triệu", "1 triệu rưỡi"
        var matchTrieu = Regex.Match(text, @"(?i)\b(\d+(?:[.,]\d+)?)\s*(?:triệu|trieu)(?:\s*(?:rưỡi|ruoi))?\b");
        if (matchTrieu.Success)
        {
            var valStr = matchTrieu.Groups[1].Value.Replace(',', '.');
            if (decimal.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                var amount = val * 1_000_000m;
                if (matchTrieu.Value.Contains("rưỡi", StringComparison.OrdinalIgnoreCase) ||
                    matchTrieu.Value.Contains("ruoi", StringComparison.OrdinalIgnoreCase))
                {
                    amount += 500_000m;
                }
                return (amount, (matchTrieu.Index, matchTrieu.Length));
            }
        }

        // 4. Pattern: "lít" / "lit" / "loét" / "loet", e.g. "2 lít" = 200,000, "3 loét" = 300,000
        var matchLit = Regex.Match(text, @"(?i)\b(\d+(?:[.,]\d+)?)\s*(?:lít|lit|loét|loet)\b");
        if (matchLit.Success)
        {
            var valStr = matchLit.Groups[1].Value.Replace(',', '.');
            if (decimal.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                return (val * 100_000m, (matchLit.Index, matchLit.Length));
            }
        }

        // 5. Pattern: "75k", "500k", "75 ngàn", "75 nghìn"
        var matchK = Regex.Match(text, @"(?i)\b(\d+(?:[.,]\d+)?)\s*(?:k|nghìn|nghin|ngàn|ngan)(?!\w)");
        if (matchK.Success)
        {
            var valStr = matchK.Groups[1].Value.Replace(',', '.');
            if (decimal.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                return (val * 1_000m, (matchK.Index, matchK.Length));
            }
        }

        // 6. Pattern: "200.000 đ", "200,000 VND", "200.000" (with thousand separators)
        var matchThousandSep = Regex.Match(text, @"(?i)\b(\d{1,3}(?:[.,]\d{3})+)\s*(?:đ|d|vnd|vnđ)?\b");
        if (matchThousandSep.Success)
        {
            var cleanStr = Regex.Replace(matchThousandSep.Groups[1].Value, @"[.,]", "");
            if (decimal.TryParse(cleanStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                return (val, (matchThousandSep.Index, matchThousandSep.Length));
            }
        }

        // 7. Pattern: Raw integer numbers with currency or standalone >= 1000, e.g. "75000", "50000 d"
        var matchRaw = Regex.Match(text, @"(?i)\b(\d{4,9})\s*(?:đ|d|vnd|vnđ)?\b");
        if (matchRaw.Success)
        {
            if (decimal.TryParse(matchRaw.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                return (val, (matchRaw.Index, matchRaw.Length));
            }
        }

        return (0, null);
    }

    private static (string? Category, (int Start, int Length)? MatchSpan) ExtractCategory(string text)
    {
        foreach (var (category, keywords) in CategoryKeywords)
        {
            foreach (var kw in keywords)
            {
                var idx = text.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return (category, (idx, kw.Length));
                }
            }
        }

        return (null, null);
    }

    private static (string? Account, (int Start, int Length)? MatchSpan) ExtractAccount(string text)
    {
        foreach (var (account, keywords) in AccountKeywords)
        {
            foreach (var kw in keywords)
            {
                // Word boundary check for short words like "mb", "vcb", "tech"
                var pattern = $@"(?i)(?:\b|(?<=[\s,]))(?:bằng\s+|qua\s+|ví\s+|the\s+|thẻ\s+)?({Regex.Escape(kw)})(?:\b|(?=[\s,]))";
                var match = Regex.Match(text, pattern);
                if (match.Success)
                {
                    return (account, (match.Index, match.Length));
                }
            }
        }

        return (null, null);
    }

    private static (DateTime? Date, (int Start, int Length)? MatchSpan) ExtractDate(string text)
    {
        if (Regex.Match(text, @"(?i)\bhôm\s+kia\b") is { Success: true } matchKia)
        {
            return (DateTime.Today.AddDays(-2), (matchKia.Index, matchKia.Length));
        }

        if (Regex.Match(text, @"(?i)\bhôm\s+qua\b") is { Success: true } matchQua)
        {
            return (DateTime.Today.AddDays(-1), (matchQua.Index, matchQua.Length));
        }

        if (Regex.Match(text, @"(?i)\bhôm\s+nay\b") is { Success: true } matchNay)
        {
            return (DateTime.Today, (matchNay.Index, matchNay.Length));
        }

        return (null, null);
    }

    private static TransactionType DetermineTransactionType(string text, string? category)
    {
        if (category == "Lương & Thu nhập")
        {
            return TransactionType.Income;
        }

        var incomeKeywords = new[] { "lương", "thưởng", "thu nhập", "nhận tiền", "hoàn tiền", "thu nợ", "bán đồ", "được cho", "tiền về" };
        if (incomeKeywords.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase)))
        {
            return TransactionType.Income;
        }

        if (text.Contains("chuyển khoản sang", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("chuyển sang", StringComparison.OrdinalIgnoreCase))
        {
            return TransactionType.Transfer;
        }

        return TransactionType.Expense;
    }

    private static string ExtractNote(string fullText, IEnumerable<(int Start, int Length)?> spansToRemove)
    {
        var sb = new StringBuilder(fullText);

        // Remove spans from back to front to preserve indexes
        var validSpans = spansToRemove
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .OrderByDescending(s => s.Start)
            .ToList();

        foreach (var span in validSpans)
        {
            if (span.Start >= 0 && span.Start + span.Length <= sb.Length)
            {
                sb.Remove(span.Start, span.Length);
            }
        }

        // Clean up common filler words like "bằng", "qua", "ví", "ngày", etc.
        var cleaned = sb.ToString();
        cleaned = Regex.Replace(cleaned, @"(?i)\b(?:bằng|qua|ví|tại|vào|ngày)\b", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(',', ' ', '.', '-');

        return string.IsNullOrWhiteSpace(cleaned) ? fullText : cleaned;
    }
}
