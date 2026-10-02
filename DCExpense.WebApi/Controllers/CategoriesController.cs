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
public class CategoriesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public CategoriesController(IApplicationDbContext context)
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

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.HouseholdId == householdId || c.HouseholdId == null)
            .Select(c => new CategoryDto(c.Id, c.Name, c.ParentId, c.DefaultBudgetType))
            .ToListAsync();

        return Ok(categories);
    }
}
