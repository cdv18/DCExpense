using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;
using DCExpense.Domain.Enums;

namespace DCExpense.WebApi.Controllers;

[ApiController]
[Route("api/households")]
public class HouseholdController : ControllerBase
{
    private readonly IHouseholdService _householdService;

    public HouseholdController(IHouseholdService householdService)
    {
        _householdService = householdService;
    }

    private string GetUserId()
    {
        var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? User?.FindFirst("sub")?.Value 
                     ?? (Request?.Headers != null && Request.Headers.TryGetValue("X-User-Id", out var headerVal) ? headerVal.FirstOrDefault() : null);

        return !string.IsNullOrWhiteSpace(userId) ? userId : "default-user";
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHouseholdDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "Household name is required." });

        var userId = GetUserId();
        var household = await _householdService.CreateHouseholdAsync(userId, dto);
        return CreatedAtAction(nameof(GetById), new { id = household.Id }, household);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyHouseholds()
    {
        var userId = GetUserId();
        var households = await _householdService.GetUserHouseholdsAsync(userId);
        return Ok(households);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetUserId();
        var household = await _householdService.GetHouseholdByIdAsync(id, userId);
        if (household == null)
            return NotFound(new { error = $"Household '{id}' not found or access denied." });

        return Ok(household);
    }

    [HttpPost("{id:guid}/invite-code")]
    public async Task<IActionResult> GenerateInviteCode(Guid id)
    {
        var userId = GetUserId();
        try
        {
            var result = await _householdService.GenerateInviteCodeAsync(id, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Household '{id}' not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinHouseholdDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.InviteCode))
            return BadRequest(new { error = "Invite code is required." });

        var userId = GetUserId();
        var success = await _householdService.JoinHouseholdByCodeAsync(userId, dto.InviteCode);
        if (!success)
            return BadRequest(new { error = "Invalid or expired invite code." });

        return Ok(new { message = "Successfully joined household." });
    }

    [HttpPut("{id:guid}/members/{userId}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, string userId, [FromBody] UpdateMemberRoleDto dto)
    {
        var requesterId = GetUserId();
        try
        {
            var success = await _householdService.UpdateMemberRoleAsync(id, userId, dto.NewRole, requesterId);
            if (!success)
                return NotFound(new { error = "Target member not found in household." });

            return Ok(new { message = "Member role updated successfully." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Household '{id}' not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/shared-wallets")]
    public async Task<IActionResult> GetSharedWallets(Guid id)
    {
        var userId = GetUserId();
        try
        {
            var wallets = await _householdService.GetSharedWalletsAsync(id, userId);
            return Ok(wallets);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }
}
