using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using DCExpense.Application.DTOs;

namespace DCExpense.Application.Interfaces;

public interface IApplicationDbContext
{
    Microsoft.EntityFrameworkCore.DbSet<DCExpense.Domain.Entities.Household> Households { get; }
    Microsoft.EntityFrameworkCore.DbSet<DCExpense.Domain.Entities.HouseholdMember> HouseholdMembers { get; }
    Microsoft.EntityFrameworkCore.DbSet<DCExpense.Domain.Entities.Account> Accounts { get; }
    Microsoft.EntityFrameworkCore.DbSet<DCExpense.Domain.Entities.Category> Categories { get; }
    Microsoft.EntityFrameworkCore.DbSet<DCExpense.Domain.Entities.Transaction> Transactions { get; }
    Microsoft.EntityFrameworkCore.DbSet<DCExpense.Domain.Entities.Budget> Budgets { get; }
    Microsoft.EntityFrameworkCore.DbSet<DCExpense.Domain.Entities.BudgetPeriod> BudgetPeriods { get; }
    Microsoft.EntityFrameworkCore.DbSet<DCExpense.Domain.Entities.Reconciliation> Reconciliations { get; }
    Microsoft.EntityFrameworkCore.DbSet<DCExpense.Domain.Entities.ThirdPartyIntegration> ThirdPartyIntegrations { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ITransactionService
{
    Task<TransactionDto> CreateTransactionAsync(CreateTransactionDto dto, CancellationToken cancellationToken = default);
    Task<IEnumerable<TransactionDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
public interface IAccountService {}
public interface IBudgetService {}
