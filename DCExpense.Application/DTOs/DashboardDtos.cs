using System;
using System.Collections.Generic;

namespace DCExpense.Application.DTOs;

public class DashboardSummaryDto
{
    public decimal TotalNetWorth { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal DailySafeToSpend { get; set; }
    public int DaysRemainingInMonth { get; set; }
    public List<AccountDto> Accounts { get; set; } = new();
    public List<BudgetProgressDto> Budgets { get; set; } = new();
    public List<CashFlowMonthDto> CashFlow { get; set; } = new();
    public List<TransactionDto> RecentTransactions { get; set; } = new();
}

public class BudgetProgressDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryType { get; set; } = string.Empty; // Needs, Wants, Savings, Education
    public decimal BudgetedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public double Percentage { get; set; }
}

public class CashFlowMonthDto
{
    public string Month { get; set; } = string.Empty;
    public int MonthNumber { get; set; }
    public int Year { get; set; }
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Net => Income - Expense;
}
