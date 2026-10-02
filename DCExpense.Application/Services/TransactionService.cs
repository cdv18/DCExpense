using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;

namespace DCExpense.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly IApplicationDbContext _context;

    public TransactionService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TransactionDto> CreateTransactionAsync(CreateTransactionDto dto, CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId, cancellationToken);

        if (account == null)
        {
            throw new KeyNotFoundException($"Account '{dto.AccountId}' was not found.");
        }

        var txDate = dto.Date == default 
            ? DateTime.UtcNow 
            : (dto.Date.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(dto.Date, DateTimeKind.Utc) 
                : dto.Date.ToUniversalTime());
        var fingerprintInput = $"{dto.AccountId}_{dto.Amount}_{txDate:yyyy-MM-dd}_{dto.Note}_{dto.BankRefId}";
        var fingerprint = GenerateSha256(fingerprintInput);

        // Anti-duplicate check
        var exists = await _context.Transactions
            .AnyAsync(t => t.AccountId == dto.AccountId && t.Fingerprint == fingerprint, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Duplicate transaction detected with identical fingerprint.");
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = dto.AccountId,
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            Date = txDate,
            Note = dto.Note ?? string.Empty,
            Type = dto.Type,
            Status = TransactionStatus.Cleared,
            BankRefId = dto.BankRefId,
            ExchangeRate = 1.0m,
            Fingerprint = fingerprint,
            CreatedByUserId = dto.CreatedByUserId
        };

        _context.Transactions.Add(transaction);

        // Process Splits if any
        if (dto.Splits != null && dto.Splits.Count > 0)
        {
            foreach (var s in dto.Splits)
            {
                var splitTx = new Transaction
                {
                    Id = Guid.NewGuid(),
                    AccountId = dto.AccountId,
                    CategoryId = s.CategoryId ?? dto.CategoryId,
                    ParentId = transaction.Id,
                    Amount = s.Amount,
                    Date = txDate,
                    Note = !string.IsNullOrWhiteSpace(s.Note) ? s.Note : transaction.Note,
                    Type = transaction.Type,
                    Status = transaction.Status,
                    ExchangeRate = 1.0m,
                    Fingerprint = GenerateSha256($"{transaction.Id}_{s.Amount}_{s.CategoryId}_{s.Note}"),
                    CreatedByUserId = dto.CreatedByUserId
                };
                _context.Transactions.Add(splitTx);
            }
        }

        // Update Account Balance
        switch (dto.Type)
        {
            case TransactionType.Expense:
            case TransactionType.Transfer:
                account.Balance -= dto.Amount;
                break;

            case TransactionType.Income:
            case TransactionType.Reimbursement:
                account.Balance += dto.Amount;
                break;

            case TransactionType.DebtPayment:
                if (account.Type == AccountType.CreditCard)
                {
                    account.Balance += dto.Amount;
                }
                else
                {
                    account.Balance -= dto.Amount;
                }
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new TransactionDto(
            transaction.Id,
            transaction.AccountId,
            transaction.CategoryId,
            transaction.Amount,
            transaction.Date,
            transaction.Note,
            transaction.Type,
            transaction.Status
        );
    }

    public async Task<IEnumerable<TransactionDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.ParentId == null)
            .OrderByDescending(t => t.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionDto(
                t.Id,
                t.AccountId,
                t.CategoryId,
                t.Amount,
                t.Date,
                t.Note,
                t.Type,
                t.Status
            ))
            .ToListAsync(cancellationToken);
    }

    private static string GenerateSha256(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
