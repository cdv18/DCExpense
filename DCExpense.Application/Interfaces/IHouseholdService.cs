using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DCExpense.Application.DTOs;
using DCExpense.Domain.Enums;

namespace DCExpense.Application.Interfaces;

public interface IHouseholdService
{
    Task<HouseholdDetailDto> CreateHouseholdAsync(string userId, CreateHouseholdDto dto);
    Task<HouseholdDetailDto?> GetHouseholdByIdAsync(Guid householdId, string userId);
    Task<IEnumerable<HouseholdDetailDto>> GetUserHouseholdsAsync(string userId);
    Task<GenerateInviteCodeResultDto> GenerateInviteCodeAsync(Guid householdId, string userId);
    Task<bool> JoinHouseholdByCodeAsync(string userId, string inviteCode);
    Task<bool> UpdateMemberRoleAsync(Guid householdId, string targetUserId, HouseholdRole newRole, string requesterUserId);
    Task<IEnumerable<AccountDto>> GetSharedWalletsAsync(Guid householdId, string userId);
}
