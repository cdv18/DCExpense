using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;

namespace DCExpense.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly IApplicationDbContext _context;

    public TransactionsController(ITransactionService transactionService, IApplicationDbContext context)
    {
        _transactionService = transactionService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
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

        var accountIds = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.HouseholdId == householdId)
            .Select(a => a.Id)
            .ToListAsync();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var items = await _context.Transactions
            .AsNoTracking()
            .Where(t => accountIds.Contains(t.AccountId) && t.ParentId == null)
            .OrderByDescending(t => t.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionDto dto)
    {
        if (dto == null)
            return BadRequest(new { error = "Invalid transaction data." });

        if (dto.Amount <= 0)
            return BadRequest(new { error = "Amount must be greater than zero." });

        var userId = User.GetCurrentUserId(Request);
        var dtoWithUser = dto with { CreatedByUserId = userId };

        try
        {
            var result = await _transactionService.CreateTransactionAsync(dtoWithUser);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
