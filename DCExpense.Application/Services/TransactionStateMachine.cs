using System;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;

namespace DCExpense.Application.Services;

public class TransactionStateMachine : ITransactionStateMachine
{
    public void ChangeState(Transaction transaction, TransactionStatus newStatus)
    {
        // Draft -> Pending -> Cleared -> Reconciled
        if (transaction.Status == newStatus) return;

        bool isValid = (transaction.Status, newStatus) switch
        {
            (TransactionStatus.Draft, TransactionStatus.Pending) => true,
            (TransactionStatus.Draft, TransactionStatus.Cleared) => true,
            (TransactionStatus.Pending, TransactionStatus.Cleared) => true,
            (TransactionStatus.Pending, TransactionStatus.Reconciled) => true,
            (TransactionStatus.Cleared, TransactionStatus.Reconciled) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException($"Invalid state transition from {transaction.Status} to {newStatus}");
        }

        transaction.Status = newStatus;
    }

    public void ValidateUpdate(Transaction existingTransaction, decimal newAmount)
    {
        if (existingTransaction.Status == TransactionStatus.Reconciled && existingTransaction.Amount != newAmount)
        {
            throw new InvalidOperationException("Cannot change the amount of a reconciled transaction.");
        }
    }
}
