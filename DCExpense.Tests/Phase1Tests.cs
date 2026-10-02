using System;
using System.Linq;
using System.Reflection;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;
using DCExpense.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCExpense.Tests
{
    public class Phase1Tests
    {
        [Fact]
        public void Domain_Entities_ShouldHaveIdAndDefaultConstraints()
        {
            var household = new Household { Name = "Test Household" };
            household.Id.Should().NotBeEmpty();
            household.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
            household.Members.Should().NotBeNull();

            var account = new Account { Name = "Test Account" };
            account.Id.Should().NotBeEmpty();
            account.Transactions.Should().NotBeNull();

            var transaction = new Transaction { Amount = 100 };
            transaction.Id.Should().NotBeEmpty();
            transaction.Status.Should().Be(TransactionStatus.Cleared);

            var category = new Category { Name = "Food" };
            category.Id.Should().NotBeEmpty();
            category.SubCategories.Should().NotBeNull();

            var budget = new Budget { Name = "Monthly Budget" };
            budget.Id.Should().NotBeEmpty();
            budget.Periods.Should().NotBeNull();

            var recon = new Reconciliation { ExpectedBalance = 100, ActualBalance = 100 };
            recon.Id.Should().NotBeEmpty();
        }

        [Fact]
        public void AccountType_Balance_Logic()
        {
            var cashAcc = new Account { Type = AccountType.Cash, Balance = 1000 };
            var bankAcc = new Account { Type = AccountType.Bank, Balance = 5000 };
            var creditAcc = new Account { Type = AccountType.CreditCard, Balance = -2000 };

            cashAcc.Type.Should().Be(AccountType.Cash);
            bankAcc.Type.Should().Be(AccountType.Bank);
            creditAcc.Type.Should().Be(AccountType.CreditCard);

            cashAcc.Balance.Should().Be(1000);
            bankAcc.Balance.Should().Be(5000);
            creditAcc.Balance.Should().Be(-2000);
        }

        [Fact]
        public void TransactionType_ParentChild_Logic()
        {
            var type = typeof(Transaction);
            var parentIdProperty = type.GetProperty("ParentId");
            
            // This will fail if ParentId is missing on Transaction, which is the expected QA finding!
            parentIdProperty.Should().NotBeNull("Transaction entity must have ParentId to support Split transactions");

            var expense = new Transaction { Type = TransactionType.Expense, Amount = 100 };
            var income = new Transaction { Type = TransactionType.Income, Amount = 200 };

            expense.Type.Should().Be(TransactionType.Expense);
            income.Type.Should().Be(TransactionType.Income);
        }

        [Fact]
        public void FingerprintEngine_Determinism_Tests()
        {
            string input1 = "TestInput123";
            string input2 = "TestInput123";
            string input3 = "TestInput124";

            var hash1 = FingerprintHelper.Generate(input1);
            var hash2 = FingerprintHelper.Generate(input2);
            var hash3 = FingerprintHelper.Generate(input3);

            hash1.Should().Be(hash2);
            hash1.Should().NotBe(hash3);
        }

        [Fact]
        public void DatabaseModel_Validation_DbSets()
        {
            var options = new DbContextOptionsBuilder<DCExpenseDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new DCExpenseDbContext(options);

            var dbSets = context.GetType().GetProperties()
                .Where(p => p.PropertyType.IsGenericType && 
                            p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .ToList();

            dbSets.Count.Should().Be(9);
            
            dbSets.Should().Contain(p => p.Name == "Households");
            dbSets.Should().Contain(p => p.Name == "HouseholdMembers");
            dbSets.Should().Contain(p => p.Name == "Accounts");
            dbSets.Should().Contain(p => p.Name == "Categories");
            dbSets.Should().Contain(p => p.Name == "Transactions");
            dbSets.Should().Contain(p => p.Name == "Budgets");
            dbSets.Should().Contain(p => p.Name == "BudgetPeriods");
            dbSets.Should().Contain(p => p.Name == "Reconciliations");
            dbSets.Should().Contain(p => p.Name == "ThirdPartyIntegrations");
        }
    }
}
