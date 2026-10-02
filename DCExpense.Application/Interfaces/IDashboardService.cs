using System.Threading;
using System.Threading.Tasks;
using DCExpense.Application.DTOs;

namespace DCExpense.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(string userId, CancellationToken cancellationToken = default);
}
