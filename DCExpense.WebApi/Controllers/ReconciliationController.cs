using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCExpense.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReconciliationController : ControllerBase
{
    private readonly IReconciliationService _reconciliationService;
    private readonly IBankStatementParser _bankStatementParser;
    private readonly IApplicationDbContext _context;

    public ReconciliationController(
        IReconciliationService reconciliationService,
        IBankStatementParser bankStatementParser,
        IApplicationDbContext context)
    {
        _reconciliationService = reconciliationService;
        _bankStatementParser = bankStatementParser;
        _context = context;
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is empty." });

        using var stream = file.OpenReadStream();
        var importedTransactions = _bankStatementParser.ParseCsv(stream).ToList();

        var existingTransactions = await _context.Transactions.ToListAsync();
        var pendingCandidates = existingTransactions.Where(t => t.Status == TransactionStatus.Pending).ToList();

        var newTransactions = new List<ImportedTransaction>();
        var autoMerged = new List<object>();
        var suspectMatches = new List<object>();

        foreach (var imported in importedTransactions)
        {
            var tempTx = new Transaction
            {
                BankRefId = imported.BankRefId,
                Date = imported.Date,
                Amount = Math.Abs(imported.Amount),
                Note = imported.Description
            };

            // 1. Deduplication
            if (_reconciliationService.CheckDuplicate(tempTx, existingTransactions))
            {
                continue; // Skip exact duplicates
            }

            // 2. Sliding Window Matcher
            var (match, score) = _reconciliationService.SlidingWindowMatch(tempTx, pendingCandidates);

            if (match != null)
            {
                if (score >= 80)
                {
                    // High confidence, auto-merge
                    match.Status = TransactionStatus.Cleared;
                    if (!string.IsNullOrEmpty(imported.BankRefId))
                        match.BankRefId = imported.BankRefId;
                    
                    autoMerged.Add(new { Imported = imported, MatchedWithId = match.Id, Score = score, MatchedTransaction = match });
                }
                else
                {
                    // Suspect match
                    suspectMatches.Add(new { Imported = imported, MatchedWithId = match.Id, Score = score, MatchedTransaction = match });
                }
            }
            else
            {
                newTransactions.Add(imported);
            }
        }

        if (autoMerged.Any())
        {
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            New = newTransactions,
            AutoMerged = autoMerged,
            Suspect = suspectMatches
        });
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] MergeRequest request)
    {
        if (request == null || request.TargetId == Guid.Empty)
            return BadRequest(new { error = "TargetId is required." });

        try
        {
            if (request.SourceId.HasValue && request.SourceId.Value != Guid.Empty)
            {
                var sourceExists = await _context.Transactions.AnyAsync(t => t.Id == request.SourceId.Value);
                if (sourceExists)
                {
                    await _reconciliationService.MergeTransactionsAsync(request.TargetId, request.SourceId.Value);
                    return Ok(new { message = "Transactions merged successfully." });
                }
            }

            // Merge imported data into target transaction
            var target = await _context.Transactions.FindAsync(request.TargetId);
            if (target == null)
            {
                return NotFound(new { error = $"Transaction '{request.TargetId}' not found." });
            }

            target.Status = TransactionStatus.Cleared;
            if (!string.IsNullOrEmpty(request.BankRefId))
                target.BankRefId = request.BankRefId;
            if (request.Amount.HasValue && request.Amount.Value != 0)
                target.Amount = Math.Abs(request.Amount.Value);
            if (request.Date.HasValue)
            {
                var d = request.Date.Value;
                target.Date = d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();
            }
            if (!string.IsNullOrEmpty(request.Note))
                target.Note = request.Note;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Transaction merged and cleared successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmRequest request)
    {
        var accountTransactions = await _context.Transactions
            .Where(t => t.AccountId == request.AccountId && t.Status == TransactionStatus.Cleared)
            .ToListAsync();

        foreach (var tx in accountTransactions)
        {
            tx.Status = TransactionStatus.Reconciled;
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Reconciled {accountTransactions.Count} transactions." });
    }
}

public class MergeRequest
{
    public Guid TargetId { get; set; }
    public Guid? SourceId { get; set; }
    public string? BankRefId { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? Date { get; set; }
    public string? Note { get; set; }
}

public class ConfirmRequest
{
    public Guid AccountId { get; set; }
}
