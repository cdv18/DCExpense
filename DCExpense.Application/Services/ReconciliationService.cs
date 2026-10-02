using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCExpense.Application.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IApplicationDbContext _context;

    public ReconciliationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public bool CheckDuplicate(Transaction imported, List<Transaction> existing)
    {
        return existing.Any(e => 
            (!string.IsNullOrEmpty(imported.BankRefId) && e.BankRefId == imported.BankRefId) ||
            (!string.IsNullOrEmpty(imported.Fingerprint) && e.Fingerprint == imported.Fingerprint)
        );
    }

    public (Transaction? Match, double ConfidenceScore) SlidingWindowMatch(Transaction imported, List<Transaction> pendingCandidates)
    {
        Transaction? bestMatch = null;
        double highestScore = 0;

        foreach (var candidate in pendingCandidates)
        {
            if (candidate.Status == TransactionStatus.Reconciled) continue;

            double score = 0;

            // 1. Amount match (exact match gives high score)
            if (candidate.Amount == imported.Amount) score += 50;
            else if (IsTranspositionError(candidate.Amount, imported.Amount)) score += 20; // Partial score for transposition

            // 2. Date match within sliding window (+/- 7 days)
            var daysDiff = Math.Abs((candidate.Date - imported.Date).TotalDays);
            if (daysDiff > 7) continue; // Exceeds sliding window, ignore candidate

            score += 30 * (1 - (daysDiff / 7.0)); // Closer date = higher score

            // 3. Description similarity (simple check for now)
            if (!string.IsNullOrEmpty(candidate.Note) && !string.IsNullOrEmpty(imported.Note))
            {
                var candidateLower = candidate.Note.ToLower();
                var importedLower = imported.Note.ToLower();
                if (candidateLower.Contains(importedLower) || importedLower.Contains(candidateLower))
                {
                    score += 20;
                }
            }

            if (score > highestScore)
            {
                highestScore = score;
                bestMatch = candidate;
            }
        }

        // Return best match if score is above a threshold, say 50
        return highestScore >= 50 ? (bestMatch, highestScore) : (null, 0);
    }

    public bool IsTranspositionError(decimal amountA, decimal amountB)
    {
        var diff = Math.Abs(amountA - amountB);
        if (diff == 0) return false;

        if (diff % 9 == 0)
        {
            var strA = Math.Abs(amountA).ToString("0.##");
            var strB = Math.Abs(amountB).ToString("0.##");
            return strA.Length == strB.Length;
        }

        return false;
    }

    public async Task MergeTransactionsAsync(Guid targetId, Guid sourceId)
    {
        var target = await _context.Transactions.FindAsync(targetId);
        var source = await _context.Transactions.FindAsync(sourceId);

        if (target == null || source == null)
            throw new ArgumentException("Transaction not found.");

        // Merge logic: target takes properties of source, source is deleted or marked as merged
        target.BankRefId = source.BankRefId ?? target.BankRefId;
        target.Fingerprint = source.Fingerprint ?? target.Fingerprint;
        target.Status = TransactionStatus.Cleared; // As per instruction
        
        // Use exact amount from bank (source)
        target.Amount = source.Amount;
        target.Date = source.Date; // Use bank's date

        if (string.IsNullOrEmpty(target.Note))
        {
            target.Note = source.Note;
        }

        _context.Transactions.Remove(source);
        await _context.SaveChangesAsync();
    }
}
