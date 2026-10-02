using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DCExpense.Application.Services;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;
using DCExpense.Infrastructure.Services;
using Xunit;

namespace DCExpense.Tests
{
    public class Phase2Tests
    {
        // 1. TransactionStateMachine_Tests
        [Fact]
        public void StateMachine_SequentialTransitions_ShouldPass()
        {
            var stateMachine = new TransactionStateMachine();
            var transaction = new Transaction { Status = TransactionStatus.Draft };

            stateMachine.ChangeState(transaction, TransactionStatus.Pending);
            Assert.Equal(TransactionStatus.Pending, transaction.Status);

            stateMachine.ChangeState(transaction, TransactionStatus.Cleared);
            Assert.Equal(TransactionStatus.Cleared, transaction.Status);

            stateMachine.ChangeState(transaction, TransactionStatus.Reconciled);
            Assert.Equal(TransactionStatus.Reconciled, transaction.Status);
        }

        [Fact]
        public void StateMachine_InvalidTransition_ShouldThrowException()
        {
            // As per instruction: "Test chặn chuyển đổi bất hợp lệ (ví dụ: Draft nhảy cóc lên Reconciled ném ngoại lệ)"
            // Currently the implementation might not throw for Draft -> Reconciled because of `newStatus > transaction.Status`, 
            // but we write the test as instructed to see if it passes or fails.
            var stateMachine = new TransactionStateMachine();
            var transaction = new Transaction { Status = TransactionStatus.Draft };

            // We test Reconciled -> Draft which is definitely invalid, or Draft -> Reconciled based on instruction.
            // Let's test Reconciled -> Draft to ensure an exception is thrown for invalid transition.
            var reconciledTransaction = new Transaction { Status = TransactionStatus.Reconciled };
            Assert.Throws<InvalidOperationException>(() => stateMachine.ChangeState(reconciledTransaction, TransactionStatus.Draft));
        }
        
        [Fact]
        public void StateMachine_DraftToReconciled_ShouldThrowException_AsPerRequirements()
        {
            // The requirement says "Draft nhảy cóc lên Reconciled ném ngoại lệ"
            var stateMachine = new TransactionStateMachine();
            var transaction = new Transaction { Status = TransactionStatus.Draft };

            Assert.Throws<InvalidOperationException>(() => stateMachine.ChangeState(transaction, TransactionStatus.Reconciled));
        }

        [Fact]
        public void StateMachine_EditReconciledTransactionAmount_ShouldThrowException()
        {
            var stateMachine = new TransactionStateMachine();
            var transaction = new Transaction { Status = TransactionStatus.Reconciled, Amount = 1000m };

            Assert.Throws<InvalidOperationException>(() => stateMachine.ValidateUpdate(transaction, 2000m));
        }

        // 2. ReconciliationService_DuplicateDetection_Tests
        [Fact]
        public void DuplicateDetection_DuplicateBankRefIdOrFingerprint_ShouldReturnTrue()
        {
            var service = new ReconciliationService(null!);
            var existing = new List<Transaction>
            {
                new Transaction { BankRefId = "REF123", Fingerprint = "HASH1" },
                new Transaction { BankRefId = "REF456", Fingerprint = "HASH2" }
            };

            var imported1 = new Transaction { BankRefId = "REF123", Fingerprint = "HASH_NEW" };
            var imported2 = new Transaction { BankRefId = "REF_NEW", Fingerprint = "HASH2" };

            Assert.True(service.CheckDuplicate(imported1, existing));
            Assert.True(service.CheckDuplicate(imported2, existing));
        }

        [Fact]
        public void DuplicateDetection_DifferentBankRefIdAndFingerprint_ShouldReturnFalse()
        {
            var service = new ReconciliationService(null!);
            var existing = new List<Transaction>
            {
                new Transaction { BankRefId = "REF123", Fingerprint = "HASH1" }
            };

            var imported = new Transaction { BankRefId = "REF999", Fingerprint = "HASH999" };

            Assert.False(service.CheckDuplicate(imported, existing));
        }

        // 3. ReconciliationService_SlidingWindowMatch_Tests
        [Fact]
        public void SlidingWindowMatch_Within7Days_ShouldFindCandidateWithHighScore()
        {
            var service = new ReconciliationService(null!);
            
            var existing = new List<Transaction>
            {
                new Transaction { Amount = 500000m, Date = new DateTime(2023, 10, 20), Status = TransactionStatus.Pending } // User entered day 20
            };
            
            var imported = new Transaction { Amount = 500000m, Date = new DateTime(2023, 10, 15) }; // Bank day 15, diff = 5 days

            var (match, score) = service.SlidingWindowMatch(imported, existing);

            Assert.NotNull(match);
            Assert.True(score > 0);
        }

        [Fact]
        public void SlidingWindowMatch_Exceeds7Days_ShouldNotMatch()
        {
            var service = new ReconciliationService(null!);
            
            var existing = new List<Transaction>
            {
                new Transaction { Amount = 500000m, Date = new DateTime(2023, 10, 30), Status = TransactionStatus.Pending } // User entered day 30
            };
            
            var imported = new Transaction { Amount = 500000m, Date = new DateTime(2023, 10, 15) }; // Bank day 15, diff = 15 days

            var (match, score) = service.SlidingWindowMatch(imported, existing);

            // With exact amount, score is 50. Wait, if score is 50, it matches (>= 50).
            // But requirement says "không match". Let's see if our test passes or fails.
            // A score of 50 is generated for amount. The days diff is > 7, so days score is 0. 
            // Total score = 50. The method says `>= 50` matches. So it might return a match!
            // I'll write the test as instructed (expecting null) to report bug if it fails.
            Assert.Null(match); 
        }

        // 4. ReconciliationService_TranspositionError_Tests
        [Fact]
        public void TranspositionError_ValidTransposition_ShouldReturnTrue()
        {
            var service = new ReconciliationService(null!);
            
            // 530k and 350k: diff 180k is divisible by 9
            bool result = service.IsTranspositionError(530000m, 350000m);
            Assert.True(result);
        }

        [Fact]
        public void TranspositionError_InvalidTransposition_ShouldReturnFalse()
        {
            var service = new ReconciliationService(null!);
            
            // 500k and 350k: diff 150k not divisible by 9
            bool result = service.IsTranspositionError(500000m, 350000m);
            Assert.False(result);
        }

        // 5. BankStatementParser_Tests
        [Fact]
        public void BankStatementParser_ShouldParseCsvCorrectly()
        {
            var parser = new BankStatementParser();
            var csvContent = "Date,Amount,Transaction ID,Description\n2023-10-15,500000,REF123,Salary\n2023-10-16,-100000,REF456,Grocery";
            
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
            var results = parser.ParseCsv(stream).ToList();

            Assert.Equal(2, results.Count);
            
            Assert.Equal(500000m, results[0].Amount);
            Assert.Equal("REF123", results[0].BankRefId);
            Assert.Equal("Salary", results[0].Description);
            
            Assert.Equal(-100000m, results[1].Amount);
            Assert.Equal("REF456", results[1].BankRefId);
            Assert.Equal("Grocery", results[1].Description);
        }
    }
}
