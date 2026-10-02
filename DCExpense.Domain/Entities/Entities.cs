using System;
using System.Collections.Generic;
using DCExpense.Domain.Enums;

namespace DCExpense.Domain.Entities;

public class Household
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? InviteCode { get; set; }
    public DateTime? InviteCodeExpiresAt { get; set; }
    public ICollection<HouseholdMember> Members { get; set; } = new List<HouseholdMember>();
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}

public class HouseholdMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string UserId { get; set; } = string.Empty; // From DCManager SSO
    public HouseholdRole Role { get; set; }
}

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsShared { get; set; } = false;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Reconciliation> Reconciliations { get; set; } = new List<Reconciliation>();
}

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? HouseholdId { get; set; }
    public Household? Household { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public BudgetType DefaultBudgetType { get; set; }
}

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public Guid? ParentId { get; set; }
    public Transaction? Parent { get; set; }
    public ICollection<Transaction> Splits { get; set; } = new List<Transaction>();
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Cleared;
    public string? BankRefId { get; set; }
    public decimal ExchangeRate { get; set; } = 1.0m;
    public string Fingerprint { get; set; } = string.Empty; // SHA-256 Hash
    public string? CreatedByUserId { get; set; }
}

public class Budget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ICollection<BudgetPeriod> Periods { get; set; } = new List<BudgetPeriod>();
}

public class BudgetPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BudgetId { get; set; }
    public Budget Budget { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Spent { get; set; }
}

public class Reconciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal ExpectedBalance { get; set; }
    public decimal ActualBalance { get; set; }
}

public class ThirdPartyIntegration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}
