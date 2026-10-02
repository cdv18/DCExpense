using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCExpense.Application.Services;

public class HouseholdService : IHouseholdService
{
    private readonly IApplicationDbContext _context;
    private const string InviteCodeChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public HouseholdService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HouseholdDetailDto> CreateHouseholdAsync(string userId, CreateHouseholdDto dto)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Household name is required.", nameof(dto));

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var ownerMember = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = userId,
            Role = HouseholdRole.Owner
        };

        var defaultSharedWallet = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Quỹ chung " + dto.Name.Trim(),
            Type = AccountType.Bank,
            Balance = 0,
            Currency = "VND",
            IsShared = true
        };

        household.Members.Add(ownerMember);
        household.Accounts.Add(defaultSharedWallet);

        _context.Households.Add(household);
        await _context.SaveChangesAsync();

        return MapToDetailDto(household);
    }

    public async Task<HouseholdDetailDto?> GetHouseholdByIdAsync(Guid householdId, string userId)
    {
        var household = await _context.Households
            .Include(h => h.Members)
            .Include(h => h.Accounts)
            .FirstOrDefaultAsync(h => h.Id == householdId);

        if (household == null)
            return null;

        var isMember = household.Members.Any(m => m.UserId == userId);
        if (!isMember)
            return null;

        return MapToDetailDto(household);
    }

    public async Task<IEnumerable<HouseholdDetailDto>> GetUserHouseholdsAsync(string userId)
    {
        var households = await _context.Households
            .Include(h => h.Members)
            .Include(h => h.Accounts)
            .Where(h => h.Members.Any(m => m.UserId == userId))
            .ToListAsync();

        return households.Select(MapToDetailDto);
    }

    public async Task<GenerateInviteCodeResultDto> GenerateInviteCodeAsync(Guid householdId, string userId)
    {
        var household = await _context.Households
            .Include(h => h.Members)
            .FirstOrDefaultAsync(h => h.Id == householdId);

        if (household == null)
            throw new KeyNotFoundException($"Household with ID '{householdId}' was not found.");

        var requester = household.Members.FirstOrDefault(m => m.UserId == userId);
        if (requester == null || (requester.Role != HouseholdRole.Owner && requester.Role != HouseholdRole.Admin))
        {
            throw new UnauthorizedAccessException("Only Owner or Admin can generate invite codes.");
        }

        var code = GenerateRandomCode(6);
        var expiresAt = DateTime.UtcNow.AddHours(48);

        household.InviteCode = code;
        household.InviteCodeExpiresAt = expiresAt;

        await _context.SaveChangesAsync();

        return new GenerateInviteCodeResultDto(code, expiresAt);
    }

    public async Task<bool> JoinHouseholdByCodeAsync(string userId, string inviteCode)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(inviteCode))
            return false;

        var cleanCode = inviteCode.Trim().ToUpperInvariant();

        var household = await _context.Households
            .Include(h => h.Members)
            .FirstOrDefaultAsync(h => h.InviteCode == cleanCode);

        if (household == null)
            return false;

        if (!household.InviteCodeExpiresAt.HasValue || household.InviteCodeExpiresAt.Value < DateTime.UtcNow)
            return false;

        var existingMember = household.Members.FirstOrDefault(m => m.UserId == userId);
        if (existingMember != null)
            return true; // Already joined

        var newMember = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = userId,
            Role = HouseholdRole.Member
        };

        _context.HouseholdMembers.Add(newMember);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UpdateMemberRoleAsync(Guid householdId, string targetUserId, HouseholdRole newRole, string requesterUserId)
    {
        var household = await _context.Households
            .Include(h => h.Members)
            .FirstOrDefaultAsync(h => h.Id == householdId);

        if (household == null)
            throw new KeyNotFoundException($"Household with ID '{householdId}' was not found.");

        var requester = household.Members.FirstOrDefault(m => m.UserId == requesterUserId);
        if (requester == null || (requester.Role != HouseholdRole.Owner && requester.Role != HouseholdRole.Admin))
        {
            throw new UnauthorizedAccessException("Only Owner or Admin can update member roles.");
        }

        var targetMember = household.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (targetMember == null)
            return false;

        // Admin cannot modify Owner role
        if (targetMember.Role == HouseholdRole.Owner && requester.Role != HouseholdRole.Owner)
        {
            throw new UnauthorizedAccessException("Admins cannot modify the role of an Owner.");
        }

        // Only Owner can promote another member to Owner
        if (newRole == HouseholdRole.Owner && requester.Role != HouseholdRole.Owner)
        {
            throw new UnauthorizedAccessException("Only an Owner can promote another member to Owner.");
        }

        targetMember.Role = newRole;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<AccountDto>> GetSharedWalletsAsync(Guid householdId, string userId)
    {
        var isMember = await _context.HouseholdMembers
            .AnyAsync(m => m.HouseholdId == householdId && m.UserId == userId);

        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this household.");

        var sharedAccounts = await _context.Accounts
            .Where(a => a.HouseholdId == householdId && a.IsShared)
            .Select(a => new AccountDto(a.Id, a.Name, a.Type, a.Balance, a.Currency))
            .ToListAsync();

        return sharedAccounts;
    }

    private static string GenerateRandomCode(int length)
    {
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            var idx = RandomNumberGenerator.GetInt32(0, InviteCodeChars.Length);
            result[i] = InviteCodeChars[idx];
        }
        return new string(result);
    }

    private static HouseholdDetailDto MapToDetailDto(Household household)
    {
        return new HouseholdDetailDto
        {
            Id = household.Id,
            Name = household.Name,
            CreatedAt = household.CreatedAt,
            InviteCode = household.InviteCode,
            InviteCodeExpiresAt = household.InviteCodeExpiresAt,
            Members = household.Members.Select(m => new HouseholdMemberDto(m.Id, m.HouseholdId, m.UserId, m.Role)).ToList(),
            Accounts = household.Accounts.Select(a => new AccountDto(a.Id, a.Name, a.Type, a.Balance, a.Currency)).ToList()
        };
    }
}
