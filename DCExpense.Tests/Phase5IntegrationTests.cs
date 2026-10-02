using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DCExpense.Application.DTOs;
using DCExpense.Application.Services;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;
using DCExpense.Infrastructure;
using DCExpense.Infrastructure.Data;
using DCExpense.Infrastructure.Services;
using DCExpense.WebApi.Controllers;
using FluentAssertions;
using Xunit;

namespace DCExpense.Tests;

public class Phase5IntegrationTests
{
    private static DCExpenseDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DCExpenseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new DCExpenseDbContext(options);
    }

    [Fact]
    public async Task SeedData_ShouldInitialize_Accounts_Categories_Budgets_And_Transactions()
    {
        using var context = CreateInMemoryDbContext();

        await DCExpenseSeedData.SeedAsync(context);

        // Households & Members
        var households = await context.Households.Include(h => h.Members).ToListAsync();
        households.Should().HaveCount(1);
        households[0].Name.Should().Be("Gia đình Kiểu Mẫu");

        var member = households[0].Members.FirstOrDefault();
        member.Should().NotBeNull();
        member!.UserId.Should().Be(DCExpenseSeedData.DefaultAdminUserId);
        member.Role.Should().Be(HouseholdRole.Owner);

        // Accounts & Net Worth
        var accounts = await context.Accounts.ToListAsync();
        accounts.Should().HaveCount(4);

        var cash = accounts.FirstOrDefault(a => a.Type == AccountType.Cash);
        var bank = accounts.FirstOrDefault(a => a.Type == AccountType.Bank);
        var momo = accounts.FirstOrDefault(a => a.Type == AccountType.EWallet);
        var credit = accounts.FirstOrDefault(a => a.Type == AccountType.CreditCard);

        cash.Should().NotBeNull();
        cash!.Balance.Should().Be(5_000_000m);
        cash.IsShared.Should().BeTrue();

        bank.Should().NotBeNull();
        bank!.Balance.Should().Be(120_000_000m);
        bank.IsShared.Should().BeTrue();

        momo.Should().NotBeNull();
        momo!.Balance.Should().Be(25_000_000m);
        momo.IsShared.Should().BeFalse();

        credit.Should().NotBeNull();
        credit!.Balance.Should().Be(-15_000_000m);
        credit.IsShared.Should().BeFalse();

        decimal totalAssets = accounts.Where(a => a.Balance > 0).Sum(a => a.Balance);
        decimal totalLiabilities = accounts.Where(a => a.Balance < 0).Sum(a => Math.Abs(a.Balance));
        decimal netWorth = totalAssets - totalLiabilities;
        netWorth.Should().Be(135_000_000m);

        // Categories
        var categories = await context.Categories.ToListAsync();
        categories.Should().HaveCount(6);
        categories.Should().Contain(c => c.Name == "Ăn uống" && c.DefaultBudgetType == BudgetType.Need);
        categories.Should().Contain(c => c.Name == "Mua sắm" && c.DefaultBudgetType == BudgetType.Want);
        categories.Should().Contain(c => c.Name == "Đi lại" && c.DefaultBudgetType == BudgetType.Need);
        categories.Should().Contain(c => c.Name == "Hóa đơn & Tiện ích" && c.DefaultBudgetType == BudgetType.Need);
        categories.Should().Contain(c => c.Name == "Lương thưởng" && c.DefaultBudgetType == BudgetType.None);
        categories.Should().Contain(c => c.Name == "Quỹ Giáo dục" && c.DefaultBudgetType == BudgetType.Savings);

        // Budgets
        var budgets = await context.Budgets.Include(b => b.Periods).ToListAsync();
        budgets.Should().HaveCount(4);
        budgets.Should().Contain(b => b.Name.Contains("50%"));
        budgets.Should().Contain(b => b.Name.Contains("30%"));
        budgets.Should().Contain(b => b.Name.Contains("20%"));
        budgets.Should().Contain(b => b.Name.Contains("JARS"));

        // Transactions
        var transactions = await context.Transactions.ToListAsync();
        transactions.Count.Should().BeGreaterThanOrEqualTo(15);

        // Idempotency: Seeding twice should not duplicate
        await DCExpenseSeedData.SeedAsync(context);
        (await context.Households.CountAsync()).Should().Be(1);
        (await context.Accounts.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task DashboardService_ShouldCalculate_AccurateNetWorth_And_SafeToSpend()
    {
        using var context = CreateInMemoryDbContext();
        await DCExpenseSeedData.SeedAsync(context);

        var service = new DashboardService(context);
        var summary = await service.GetDashboardSummaryAsync(DCExpenseSeedData.DefaultAdminUserId);

        summary.Should().NotBeNull();
        summary.TotalNetWorth.Should().Be(135_000_000m);
        summary.TotalAssets.Should().Be(150_000_000m);
        summary.TotalLiabilities.Should().Be(15_000_000m);
        summary.DailySafeToSpend.Should().BeGreaterThan(0);
        summary.DaysRemainingInMonth.Should().BeGreaterThan(0);

        summary.Accounts.Should().HaveCount(4);
        summary.Budgets.Should().HaveCount(4);
        summary.CashFlow.Should().HaveCount(5);
        summary.RecentTransactions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TransactionService_ShouldCreateTransaction_And_UpdateAccountBalance()
    {
        using var context = CreateInMemoryDbContext();
        await DCExpenseSeedData.SeedAsync(context);

        var txService = new TransactionService(context);
        var cashAccount = await context.Accounts.FirstAsync(a => a.Type == AccountType.Cash);
        var initialBalance = cashAccount.Balance;

        // 1. Create Expense
        var expenseDto = new CreateTransactionDto(
            AccountId: cashAccount.Id,
            CategoryId: null,
            Amount: 200_000m,
            Date: DateTime.UtcNow,
            Note: "Mua đồ ăn vặt",
            Type: TransactionType.Expense
        );

        var created = await txService.CreateTransactionAsync(expenseDto);
        created.Should().NotBeNull();
        created.Amount.Should().Be(200_000m);

        var updatedAccount = await context.Accounts.FindAsync(cashAccount.Id);
        updatedAccount!.Balance.Should().Be(initialBalance - 200_000m);

        // 2. Anti-duplicate check with identical fingerprint
        var duplicateFunc = async () => await txService.CreateTransactionAsync(expenseDto);
        await duplicateFunc.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate*");

        // 3. Create Income
        var incomeDto = new CreateTransactionDto(
            AccountId: cashAccount.Id,
            CategoryId: null,
            Amount: 500_000m,
            Date: DateTime.UtcNow.AddMinutes(5),
            Note: "Bán đồ cũ",
            Type: TransactionType.Income
        );

        await txService.CreateTransactionAsync(incomeDto);
        updatedAccount = await context.Accounts.FindAsync(cashAccount.Id);
        updatedAccount!.Balance.Should().Be(initialBalance - 200_000m + 500_000m);
    }

    [Fact]
    public async Task TransactionService_ShouldHandle_SplitTransactions()
    {
        using var context = CreateInMemoryDbContext();
        await DCExpenseSeedData.SeedAsync(context);

        var txService = new TransactionService(context);
        var bankAccount = await context.Accounts.FirstAsync(a => a.Type == AccountType.Bank);
        var initialBalance = bankAccount.Balance;

        var splits = new List<CreateSplitItemDto>
        {
            new(300_000m, null, "Đồ uống"),
            new(200_000m, null, "Đồ ăn nhẹ")
        };

        var splitTxDto = new CreateTransactionDto(
            AccountId: bankAccount.Id,
            CategoryId: null,
            Amount: 500_000m,
            Date: DateTime.UtcNow,
            Note: "Siêu thị tổng hợp",
            Type: TransactionType.Expense,
            Splits: splits
        );

        var result = await txService.CreateTransactionAsync(splitTxDto);
        result.Should().NotBeNull();

        // Check DB for child splits
        var childSplits = await context.Transactions
            .Where(t => t.ParentId == result.Id)
            .ToListAsync();

        childSplits.Should().HaveCount(2);
        childSplits.Sum(c => c.Amount).Should().Be(500_000m);

        var updatedAccount = await context.Accounts.FindAsync(bankAccount.Id);
        updatedAccount!.Balance.Should().Be(initialBalance - 500_000m);
    }

    [Fact]
    public void ReconciliationService_And_Parser_ShouldClassifyBankStatement()
    {
        var parser = new BankStatementParser();
        var reconciliationService = new ReconciliationService(null!);

        var csvContent = "Date,Amount,Transaction ID,Description\n" +
                         "2026-10-01,-1200000,VCB1001,Thanh toán tiền điện\n" +
                         "2026-10-05,-450000,VCB1005,THANH TOAN SHOPEE\n" +
                         "2026-10-04,-50000,VCB1004,CHUYEN TIEN CAFE\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var imported = parser.ParseCsv(stream).ToList();

        imported.Should().HaveCount(3);

        // Pending candidates in system
        var pendingCandidates = new List<Transaction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Amount = 1_200_000m,
                Date = new DateTime(2026, 10, 1),
                Note = "Thanh toán tiền điện",
                Status = TransactionStatus.Pending
            },
            new()
            {
                Id = Guid.NewGuid(),
                Amount = 450_000m,
                Date = new DateTime(2026, 10, 2),
                Note = "Mua sắm Shopee",
                Status = TransactionStatus.Pending
            }
        };

        // Match #1: Exact amount, exact date, exact note -> High score >= 80
        var tx1 = new Transaction
        {
            Amount = Math.Abs(imported[0].Amount),
            Date = imported[0].Date,
            Note = imported[0].Description
        };
        var (match1, score1) = reconciliationService.SlidingWindowMatch(tx1, pendingCandidates);
        match1.Should().NotBeNull();
        score1.Should().BeGreaterThanOrEqualTo(80);

        // Match #2: Exact amount, 3 days date diff, similar note -> Suspect match
        var tx2 = new Transaction
        {
            Amount = Math.Abs(imported[1].Amount),
            Date = imported[1].Date,
            Note = imported[1].Description
        };
        var (match2, score2) = reconciliationService.SlidingWindowMatch(tx2, pendingCandidates);
        match2.Should().NotBeNull();
        score2.Should().BeGreaterThanOrEqualTo(50);

        // Match #3: New transaction with no pending candidate
        var tx3 = new Transaction
        {
            Amount = Math.Abs(imported[2].Amount),
            Date = imported[2].Date,
            Note = imported[2].Description
        };
        var (match3, score3) = reconciliationService.SlidingWindowMatch(tx3, pendingCandidates);
        match3.Should().BeNull();
        score3.Should().Be(0);
    }

    [Fact]
    public async Task ReconciliationController_Merge_ShouldClearTransaction()
    {
        using var context = CreateInMemoryDbContext();
        await DCExpenseSeedData.SeedAsync(context);

        var parser = new BankStatementParser();
        var reconService = new ReconciliationService(context);
        var controller = new ReconciliationController(reconService, parser, context);

        var pendingTx = await context.Transactions.FirstOrDefaultAsync(t => t.Status == TransactionStatus.Pending);
        pendingTx.Should().NotBeNull();

        var mergeReq = new MergeRequest
        {
            TargetId = pendingTx!.Id,
            BankRefId = "VCB_MERGED_REF",
            Amount = pendingTx.Amount,
            Date = pendingTx.Date,
            Note = "Đã gộp từ sao kê"
        };

        var result = await controller.Merge(mergeReq);
        result.Should().BeOfType<OkObjectResult>();

        var updated = await context.Transactions.FindAsync(pendingTx.Id);
        updated!.Status.Should().Be(TransactionStatus.Cleared);
        updated.BankRefId.Should().Be("VCB_MERGED_REF");
    }

    [Fact]
    public async Task WebApiControllers_Dashboard_Accounts_Categories_Transactions_ShouldReturnOk()
    {
        using var context = CreateInMemoryDbContext();
        await DCExpenseSeedData.SeedAsync(context);

        var dashService = new DashboardService(context);
        var txService = new TransactionService(context);

        var dashCtrl = new DashboardController(dashService);
        var accCtrl = new AccountsController(context);
        var catCtrl = new CategoriesController(context);
        var txCtrl = new TransactionsController(txService, context);

        // Dashboard
        var dashRes = await dashCtrl.GetSummary() as OkObjectResult;
        dashRes.Should().NotBeNull();
        dashRes!.Value.Should().BeOfType<DashboardSummaryDto>();

        // Accounts
        var accRes = await accCtrl.Get() as OkObjectResult;
        accRes.Should().NotBeNull();
        var accList = accRes!.Value as IEnumerable<AccountDto>;
        accList.Should().HaveCount(4);

        // Categories
        var catRes = await catCtrl.Get() as OkObjectResult;
        catRes.Should().NotBeNull();
        var catList = catRes!.Value as IEnumerable<CategoryDto>;
        catList.Should().HaveCount(6);

        // Transactions
        var txRes = await txCtrl.Get(1, 20) as OkObjectResult;
        txRes.Should().NotBeNull();
        var txList = txRes!.Value as IEnumerable<TransactionDto>;
        txList.Should().NotBeEmpty();
    }
}
