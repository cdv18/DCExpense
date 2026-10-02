using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;

namespace DCExpense.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AccountsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = User.GetCurrentUserId(Request);
        
        var householdId = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.HouseholdId)
            .FirstOrDefaultAsync();

        if (householdId == Guid.Empty)
        {
            var firstHousehold = await _context.Households.AsNoTracking().FirstOrDefaultAsync();
            if (firstHousehold != null)
            {
                householdId = firstHousehold.Id;
            }
        }

        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.HouseholdId == householdId)
            .Select(a => new AccountDto(a.Id, a.Name, a.Type, a.Balance, a.Currency))
            .ToListAsync();

        return Ok(accounts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null)
            return NotFound(new { error = $"Account '{id}' was not found." });

        return Ok(new AccountDto(account.Id, account.Name, account.Type, account.Balance, account.Currency));
    }
}
