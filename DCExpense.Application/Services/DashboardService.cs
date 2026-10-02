using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;

namespace DCExpense.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;

    public DashboardService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 1. Resolve Household for user
        Guid householdId = Guid.Empty;
        var member = await _context.HouseholdMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

        if (member != null)
        {
            householdId = member.HouseholdId;
        }
        else
        {
            // Fallback to first household or empty
            var firstHousehold = await _context.Households
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
            if (firstHousehold != null)
            {
                householdId = firstHousehold.Id;
            }
        }

        // 2. Fetch Accounts
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.HouseholdId == householdId)
            .ToListAsync(cancellationToken);

        var accountDtos = accounts.Select(a => new AccountDto(
            a.Id,
            a.Name,
            a.Type,
            a.Balance,
            a.Currency
        )).ToList();

        // Calculate Net Worth: Assets - Liabilities
        decimal totalAssets = accounts.Where(a => a.Balance > 0).Sum(a => a.Balance);
        decimal totalLiabilities = accounts.Where(a => a.Balance < 0).Sum(a => Math.Abs(a.Balance));
        decimal netWorth = totalAssets - totalLiabilities;

        // 3. Budgets & Safe-To-Spend
        var now = DateTime.UtcNow;
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var remainingDays = Math.Max(1, daysInMonth - now.Day + 1);

        var budgets = await _context.Budgets
            .AsNoTracking()
            .Include(b => b.Periods)
            .Where(b => b.HouseholdId == householdId)
            .ToListAsync(cancellationToken);

        var budgetProgressList = new List<BudgetProgressDto>();
        decimal wantsRemaining = 0m;
        bool hasWantsBudget = false;

        foreach (var b in budgets)
        {
            var period = b.Periods
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefault();

            decimal spent = period?.Spent ?? 0m;
            double percentage = b.Amount > 0 ? (double)Math.Round((spent / b.Amount) * 100, 1) : 0;

            string catType = "Needs";
            if (b.Name.Contains("Mong muốn") || b.Name.Contains("Wants", StringComparison.OrdinalIgnoreCase))
            {
                catType = "Wants";
                wantsRemaining = Math.Max(0, b.Amount - spent);
                hasWantsBudget = true;
            }
            else if (b.Name.Contains("Tiết kiệm") || b.Name.Contains("Savings", StringComparison.OrdinalIgnoreCase))
            {
                catType = "Savings";
            }
            else if (b.Name.Contains("Giáo dục") || b.Name.Contains("EDU", StringComparison.OrdinalIgnoreCase))
            {
                catType = "Education";
            }

            budgetProgressList.Add(new BudgetProgressDto
            {
                Id = b.Id,
                Name = b.Name,
                CategoryType = catType,
                BudgetedAmount = b.Amount,
                SpentAmount = spent,
                Percentage = percentage
            });
        }

        // Safe to spend today calculation:
        // Use remaining discretionary budget if defined; otherwise liquid accounts (Cash & E-Wallet) / remaining days
        decimal safeFund = hasWantsBudget && wantsRemaining > 0 
            ? wantsRemaining 
            : accounts.Where(a => a.Type == AccountType.Cash || a.Type == AccountType.EWallet).Sum(a => Math.Max(0, a.Balance));

        decimal dailySafeToSpend = remainingDays > 0 ? Math.Round(safeFund / remainingDays, 0) : safeFund;

        // 4. Cash Flow (Last 5 Months)
        var accountIds = accounts.Select(a => a.Id).ToHashSet();
        var cashFlowList = new List<CashFlowMonthDto>();

        // Find last 5 months
        for (int i = 4; i >= 0; i--)
        {
            var monthDate = now.AddMonths(-i);
            var startOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            var monthTxs = await _context.Transactions
                .AsNoTracking()
                .Where(t => accountIds.Contains(t.AccountId) && 
                            t.Date >= startOfMonth && 
                            t.Date <= endOfMonth && 
                            t.ParentId == null &&
                            t.Status != TransactionStatus.Draft)
                .ToListAsync(cancellationToken);

            decimal income = monthTxs.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            decimal expense = monthTxs.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

            cashFlowList.Add(new CashFlowMonthDto
            {
                Month = $"Tháng {monthDate.Month}",
                MonthNumber = monthDate.Month,
                Year = monthDate.Year,
                Income = income,
                Expense = expense
            });
        }

        // 5. Recent Transactions
        var recentTxs = await _context.Transactions
            .AsNoTracking()
            .Where(t => accountIds.Contains(t.AccountId) && t.ParentId == null)
            .OrderByDescending(t => t.Date)
            .Take(20)
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

        return new DashboardSummaryDto
        {
            TotalNetWorth = netWorth,
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            DailySafeToSpend = dailySafeToSpend,
            DaysRemainingInMonth = remainingDays,
            Accounts = accountDtos,
            Budgets = budgetProgressList,
            CashFlow = cashFlowList,
            RecentTransactions = recentTxs
        };
    }
}
