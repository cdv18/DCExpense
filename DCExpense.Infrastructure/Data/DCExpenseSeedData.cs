using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;

namespace DCExpense.Infrastructure.Data;

public static class DCExpenseSeedData
{
    public const string DefaultAdminUserId = "718680ab-f7cb-41f2-b8c2-4fc9e2ad4594";

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DCExpenseDbContext>();
        await SeedAsync(context);
    }

    public static async Task SeedAsync(DCExpenseDbContext context)
    {
        if (await context.Households.AnyAsync())
        {
            return; // Data already seeded
        }

        // 1. Household
        var household = new Household
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Gia đình Kiểu Mẫu",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Households.Add(household);

        // 2. HouseholdMember
        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = DefaultAdminUserId,
            Role = HouseholdRole.Owner
        };
        context.HouseholdMembers.Add(member);

        // 3. Accounts
        var cashAccount = new Account
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222221"),
            HouseholdId = household.Id,
            Name = "Ví Tiền Mặt",
            Type = AccountType.Cash,
            Balance = 5_000_000m,
            Currency = "VND",
            IsShared = true
        };

        var bankAccount = new Account
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            HouseholdId = household.Id,
            Name = "Tài khoản Vietcombank",
            Type = AccountType.Bank,
            Balance = 120_000_000m,
            Currency = "VND",
            IsShared = true
        };

        var momoAccount = new Account
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222223"),
            HouseholdId = household.Id,
            Name = "Ví MoMo",
            Type = AccountType.EWallet,
            Balance = 25_000_000m,
            Currency = "VND",
            IsShared = false
        };

        var creditCardAccount = new Account
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222224"),
            HouseholdId = household.Id,
            Name = "Thẻ tín dụng Techcombank",
            Type = AccountType.CreditCard,
            Balance = -15_000_000m,
            Currency = "VND",
            IsShared = false
        };

        context.Accounts.AddRange(cashAccount, bankAccount, momoAccount, creditCardAccount);

        // 4. Categories
        var catFood = new Category
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333331"),
            HouseholdId = household.Id,
            Name = "Ăn uống",
            DefaultBudgetType = BudgetType.Need
        };

        var catShopping = new Category
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333332"),
            HouseholdId = household.Id,
            Name = "Mua sắm",
            DefaultBudgetType = BudgetType.Want
        };

        var catTransport = new Category
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            HouseholdId = household.Id,
            Name = "Đi lại",
            DefaultBudgetType = BudgetType.Need
        };

        var catBills = new Category
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333334"),
            HouseholdId = household.Id,
            Name = "Hóa đơn & Tiện ích",
            DefaultBudgetType = BudgetType.Need
        };

        var catSalary = new Category
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333335"),
            HouseholdId = household.Id,
            Name = "Lương thưởng",
            DefaultBudgetType = BudgetType.None
        };

        var catEducation = new Category
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333336"),
            HouseholdId = household.Id,
            Name = "Quỹ Giáo dục",
            DefaultBudgetType = BudgetType.Savings
        };

        context.Categories.AddRange(catFood, catShopping, catTransport, catBills, catSalary, catEducation);

        // 5. Budgets & BudgetPeriods (50/30/20 & JARS for October 2026)
        var budgetNeeds = new Budget
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Nhu cầu thiết yếu (Needs - 50%)",
            Amount = 15_000_000m,
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
            Periods = new List<BudgetPeriod>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
                    Spent = 8_200_000m
                }
            }
        };

        var budgetWants = new Budget
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Mong muốn (Wants - 30%)",
            Amount = 9_000_000m,
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
            Periods = new List<BudgetPeriod>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
                    Spent = 5_400_000m
                }
            }
        };

        var budgetSavings = new Budget
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Tiết kiệm & Đầu tư (Savings - 20%)",
            Amount = 6_000_000m,
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
            Periods = new List<BudgetPeriod>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
                    Spent = 4_000_000m
                }
            }
        };

        var budgetEducation = new Budget
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Quỹ Giáo dục (EDU / JARS)",
            Amount = 3_000_000m,
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
            Periods = new List<BudgetPeriod>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
                    Spent = 2_000_000m
                }
            }
        };

        context.Budgets.AddRange(budgetNeeds, budgetWants, budgetSavings, budgetEducation);

        // 6. 18 Sample Transactions (Sept & Oct 2026)
        var transactions = new List<Transaction>
        {
            // September 2026
            CreateTx(bankAccount.Id, catSalary.Id, 30_000_000m, new DateTime(2026, 9, 5, 8, 30, 0, DateTimeKind.Utc),
                "Lương tháng 9/2026", TransactionType.Income, TransactionStatus.Cleared, "VCB-SALARY-202609"),

            CreateTx(bankAccount.Id, catBills.Id, 1_450_000m, new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc),
                "Tiền điện nước tháng 8", TransactionType.Expense, TransactionStatus.Cleared, "VCB-BILL-20260906"),

            CreateTx(cashAccount.Id, catFood.Id, 850_000m, new DateTime(2026, 9, 8, 17, 30, 0, DateTimeKind.Utc),
                "Đi siêu thị WinMart", TransactionType.Expense, TransactionStatus.Cleared),

            CreateTx(creditCardAccount.Id, catShopping.Id, 1_200_000m, new DateTime(2026, 9, 12, 19, 0, 0, DateTimeKind.Utc),
                "Mua sắm quần áo Uniqlo", TransactionType.Expense, TransactionStatus.Cleared, "TCB-UNIQLO-0912"),

            CreateTx(cashAccount.Id, catTransport.Id, 100_000m, new DateTime(2026, 9, 15, 7, 45, 0, DateTimeKind.Utc),
                "Đổ xăng xe máy", TransactionType.Expense, TransactionStatus.Cleared),

            CreateTx(bankAccount.Id, catFood.Id, 1_600_000m, new DateTime(2026, 9, 20, 19, 30, 0, DateTimeKind.Utc),
                "Ăn tối gia đình nhà hàng", TransactionType.Expense, TransactionStatus.Cleared, "VCB-DINNER-0920"),

            CreateTx(momoAccount.Id, catEducation.Id, 2_500_000m, new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc),
                "Khóa học lập trình trực tuyến", TransactionType.Expense, TransactionStatus.Cleared, "MOMO-EDU-0925"),

            CreateTx(momoAccount.Id, catShopping.Id, 150_000m, new DateTime(2026, 9, 28, 14, 20, 0, DateTimeKind.Utc),
                "Cafe cùng đồng nghiệp", TransactionType.Expense, TransactionStatus.Cleared, "MOMO-CAFE-0928"),

            // October 2026
            CreateTx(bankAccount.Id, catBills.Id, 1_200_000m, new DateTime(2026, 10, 1, 9, 15, 0, DateTimeKind.Utc),
                "Thanh toán tiền điện", TransactionType.Expense, TransactionStatus.Pending),

            CreateTx(momoAccount.Id, catShopping.Id, 450_000m, new DateTime(2026, 10, 2, 11, 30, 0, DateTimeKind.Utc),
                "Mua sắm Shopee", TransactionType.Expense, TransactionStatus.Pending),

            CreateTx(cashAccount.Id, catFood.Id, 70_000m, new DateTime(2026, 10, 3, 7, 15, 0, DateTimeKind.Utc),
                "Ăn sáng phở bò", TransactionType.Expense, TransactionStatus.Cleared),

            CreateTx(momoAccount.Id, catTransport.Id, 120_000m, new DateTime(2026, 10, 4, 18, 0, 0, DateTimeKind.Utc),
                "Đi lại GrabCar", TransactionType.Expense, TransactionStatus.Cleared, "GRAB-1004"),

            CreateTx(bankAccount.Id, catSalary.Id, 35_000_000m, new DateTime(2026, 10, 5, 8, 30, 0, DateTimeKind.Utc),
                "Nhận lương tháng 10/2026", TransactionType.Income, TransactionStatus.Cleared, "VCB-SALARY-202610"),

            CreateTx(bankAccount.Id, catEducation.Id, 350_000m, new DateTime(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc),
                "Mua sách kỹ năng sống", TransactionType.Expense, TransactionStatus.Cleared, "VCB-BOOK-1006"),

            CreateTx(creditCardAccount.Id, catFood.Id, 980_000m, new DateTime(2026, 10, 7, 16, 45, 0, DateTimeKind.Utc),
                "Đi siêu thị Big C", TransactionType.Expense, TransactionStatus.Cleared, "TCB-BIGC-1007"),

            CreateTx(bankAccount.Id, catBills.Id, 330_000m, new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc),
                "Đóng tiền internet FPT", TransactionType.Expense, TransactionStatus.Cleared, "VCB-FPT-1008"),

            CreateTx(creditCardAccount.Id, catShopping.Id, 1_500_000m, new DateTime(2026, 10, 9, 20, 10, 0, DateTimeKind.Utc),
                "Mua tai nghe Bluetooth", TransactionType.Expense, TransactionStatus.Cleared, "TCB-AUDIO-1009"),

            CreateTx(cashAccount.Id, catTransport.Id, 90_000m, new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc),
                "Đổ xăng xe máy", TransactionType.Expense, TransactionStatus.Cleared)
        };

        context.Transactions.AddRange(transactions);
        await context.SaveChangesAsync();
    }

    private static Transaction CreateTx(
        Guid accountId, 
        Guid categoryId, 
        decimal amount, 
        DateTime date, 
        string note, 
        TransactionType type, 
        TransactionStatus status, 
        string? bankRefId = null)
    {
        var input = $"{accountId}_{amount}_{date:yyyy-MM-dd}_{note}_{bankRefId}";
        return new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CategoryId = categoryId,
            Amount = amount,
            Date = date,
            Note = note,
            Type = type,
            Status = status,
            BankRefId = bankRefId,
            ExchangeRate = 1.0m,
            Fingerprint = FingerprintHelper.Generate(input),
            CreatedByUserId = DefaultAdminUserId
        };
    }
}
