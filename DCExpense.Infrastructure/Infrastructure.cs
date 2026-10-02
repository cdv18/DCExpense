using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using DCExpense.Domain.Entities;
using DCExpense.Application.Interfaces;

namespace DCExpense.Infrastructure;

public class DCExpenseDbContext : DbContext, IApplicationDbContext
{
    public DCExpenseDbContext(DbContextOptions<DCExpenseDbContext> options) : base(options) { }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
    public DbSet<ThirdPartyIntegration> ThirdPartyIntegrations => Set<ThirdPartyIntegration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("dcexpense");
        
        modelBuilder.Entity<Household>().ToTable("Households");
        modelBuilder.Entity<HouseholdMember>().ToTable("HouseholdMembers");
        modelBuilder.Entity<Account>().ToTable("Accounts");
        modelBuilder.Entity<Category>().ToTable("Categories");
        modelBuilder.Entity<Transaction>().ToTable("Transactions");
        modelBuilder.Entity<Budget>().ToTable("Budgets");
        modelBuilder.Entity<BudgetPeriod>().ToTable("BudgetPeriods");
        modelBuilder.Entity<Reconciliation>().ToTable("Reconciliations");
        modelBuilder.Entity<ThirdPartyIntegration>().ToTable("ThirdPartyIntegrations");
    }
}

public static class FingerprintHelper
{
    public static string Generate(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
