using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;

namespace DCExpense.Application.Interfaces;

public interface ITransactionStateMachine
{
    void ChangeState(Transaction transaction, TransactionStatus newStatus);
    void ValidateUpdate(Transaction existingTransaction, decimal newAmount);
}

public interface IReconciliationService
{
    bool CheckDuplicate(Transaction imported, List<Transaction> existing);
    (Transaction? Match, double ConfidenceScore) SlidingWindowMatch(Transaction imported, List<Transaction> pendingCandidates);
    bool IsTranspositionError(decimal amountA, decimal amountB);
    Task MergeTransactionsAsync(Guid targetId, Guid sourceId);
}

public class ImportedTransaction
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? BankRefId { get; set; }
}

public interface IBankStatementParser
{
    IEnumerable<ImportedTransaction> ParseCsv(System.IO.Stream csvStream);
}
