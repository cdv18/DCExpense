using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using DCExpense.Application.DTOs;
using DCExpense.Application.Interfaces;
using DCExpense.Application.Services;
using DCExpense.Domain.Entities;
using DCExpense.Domain.Enums;
using DCExpense.Infrastructure;
using DCExpense.Infrastructure.Services;
using DCExpense.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DCExpense.Tests;

public class Phase4Tests
{
    private static DCExpenseDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DCExpenseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new DCExpenseDbContext(options);
    }

    private static HouseholdController CreateHouseholdControllerWithUser(IHouseholdService service, string userId)
    {
        var controller = new HouseholdController(service);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = userId;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    #region AI Voice Expense Parser Tests

    [Theory]
    [InlineData("Ăn phở 75k", 75000, "Ăn uống")]
    [InlineData("Cơm trưa 75000 tiền mặt", 75000, "Ăn uống")]
    [InlineData("Đổ xăng 75 ngàn vcb", 75000, "Đi lại")]
    [InlineData("Mua vé 75 nghìn", 75000, null)]
    [InlineData("1.2 củ mua vé máy bay techcombank", 1200000, "Đi lại")]
    [InlineData("Gửi xe 2 lít momo", 200000, "Đi lại")]
    [InlineData("Mua sắm 3 loét zalopay", 300000, "Mua sắm")]
    [InlineData("1tr5 tiền nhà", 1500000, "Tiện ích")]
    [InlineData("Tiền điện 500k hôm qua", 500000, "Tiện ích")]
    [InlineData("Đi siêu thị 200.000 đ", 200000, "Siêu thị")]
    [InlineData("1 củ 2", 1200000, null)]
    [InlineData("3 loét", 300000, null)]
    [InlineData("1 triệu rưỡi", 1500000, null)]
    [InlineData("50k ăn sáng qua ví momo", 50000, "Ăn uống")]
    public void AiParser_VietnameseAmountAndCategory_ShouldParseCorrectly(string input, decimal expectedAmount, string? expectedCategory)
    {
        var parser = new AiVoiceExpenseParser();
        var result = parser.ParseExpenseText(input);

        result.IsSuccess.Should().BeTrue();
        result.Amount.Should().Be(expectedAmount);
        if (expectedCategory != null)
        {
            result.CategoryName.Should().Be(expectedCategory);
        }
    }

    [Fact]
    public void AiParser_EdgeCases_SlangAndComplexFormats_DetailedVerification()
    {
        var parser = new AiVoiceExpenseParser();

        // 1. "1 củ 2" -> 1,200,000 VND
        var resCu = parser.ParseExpenseText("1 củ 2");
        resCu.IsSuccess.Should().BeTrue();
        resCu.Amount.Should().Be(1200000m);

        // 2. "3 loét" -> 300,000 VND
        var resLoet = parser.ParseExpenseText("3 loét");
        resLoet.IsSuccess.Should().BeTrue();
        resLoet.Amount.Should().Be(300000m);

        // 3. "1 triệu rưỡi" -> 1,500,000 VND
        var resRuoi = parser.ParseExpenseText("1 triệu rưỡi");
        resRuoi.IsSuccess.Should().BeTrue();
        resRuoi.Amount.Should().Be(1500000m);

        // 4. "50k ăn sáng qua ví momo" -> Amount: 50,000, Category: Ăn uống, Account: Momo, Note: ăn sáng
        var resComplex = parser.ParseExpenseText("50k ăn sáng qua ví momo");
        resComplex.IsSuccess.Should().BeTrue();
        resComplex.Amount.Should().Be(50000m);
        resComplex.CategoryName.Should().Be("Ăn uống");
        resComplex.AccountName.Should().Be("Momo");
        resComplex.Note.Should().Be("ăn sáng");
    }

    [Theory]
    [InlineData("Ăn sáng cùng đồng nghiệp")]
    [InlineData("Họp nhóm cafe highland")]
    [InlineData("Chuyển tiền ví momo")]
    [InlineData("")]
    [InlineData("   ")]
    public void AiParser_NoAmount_ShouldReturnIsSuccessFalse(string input)
    {
        var parser = new AiVoiceExpenseParser();
        var result = parser.ParseExpenseText(input);

        result.IsSuccess.Should().BeFalse();
        result.Amount.Should().Be(0);
    }

    [Fact]
    public void AiParser_Accounts_ShouldRecognizeCommonWallets()
    {
        var parser = new AiVoiceExpenseParser();

        var resCash = parser.ParseExpenseText("Cà phê 40k tiền mặt");
        resCash.AccountName.Should().Be("Tiền mặt");

        var resVcb = parser.ParseExpenseText("Chuyển khoản 100k vcb");
        resVcb.AccountName.Should().Be("Vietcombank");

        var resMomo = parser.ParseExpenseText("Trà sữa 60k momo");
        resMomo.AccountName.Should().Be("Momo");

        var resZalo = parser.ParseExpenseText("Mua bánh mì 25k zalopay");
        resZalo.AccountName.Should().Be("ZaloPay");

        var resTech = parser.ParseExpenseText("Đổ xăng 80k techcombank");
        resTech.AccountName.Should().Be("Techcombank");

        var resVisa = parser.ParseExpenseText("Ăn tối pizza 300k thẻ tín dụng");
        resVisa.AccountName.Should().Be("Thẻ tín dụng / Visa");
    }

    [Fact]
    public void AiParser_Dates_ShouldParseRelativeDates()
    {
        var parser = new AiVoiceExpenseParser();

        var resToday = parser.ParseExpenseText("Ăn trưa 50k hôm nay");
        resToday.TransactionDate?.Date.Should().Be(DateTime.Today);

        var resYesterday = parser.ParseExpenseText("Đổ xăng 60k hôm qua");
        resYesterday.TransactionDate?.Date.Should().Be(DateTime.Today.AddDays(-1));

        var resDayBefore = parser.ParseExpenseText("Mua sách 120k hôm kia");
        resDayBefore.TransactionDate?.Date.Should().Be(DateTime.Today.AddDays(-2));
    }

    [Fact]
    public void AiParser_IncomeType_ShouldDetectIncome()
    {
        var parser = new AiVoiceExpenseParser();
        var result = parser.ParseExpenseText("Nhận tiền lương 15tr vcb hôm nay");

        result.IsSuccess.Should().BeTrue();
        result.Amount.Should().Be(15000000m);
        result.Type.Should().Be(TransactionType.Income);
    }

    [Fact]
    public async Task AiParser_VoiceAudioAsync_ShouldParseFromStream()
    {
        var parser = new AiVoiceExpenseParser();
        var voiceText = "Cà phê sữa 35k tiền mặt";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(voiceText));

        var result = await parser.ParseVoiceAudioAsync(stream, "test_voice.ogg");

        result.IsSuccess.Should().BeTrue();
        result.Amount.Should().Be(35000m);
        result.AccountName.Should().Be("Tiền mặt");
        result.CategoryName.Should().Be("Ăn uống");
    }

    #endregion

    #region Household Service Tests

    [Fact]
    public async Task HouseholdService_CreateHousehold_ShouldCreateOwnerAndSharedWallet()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var userId = "user-123";
        var result = await service.CreateHouseholdAsync(userId, new CreateHouseholdDto("Gia đình Hạnh Phúc"));

        result.Should().NotBeNull();
        result.Name.Should().Be("Gia đình Hạnh Phúc");
        result.Members.Should().ContainSingle(m => m.UserId == userId && m.Role == HouseholdRole.Owner);
        result.Accounts.Should().ContainSingle(a => a.Name.Contains("Gia đình Hạnh Phúc"));

        var dbHousehold = await context.Households.Include(h => h.Accounts).FirstOrDefaultAsync(h => h.Id == result.Id);
        dbHousehold.Should().NotBeNull();
        dbHousehold!.Accounts.First().IsShared.Should().BeTrue();
    }

    [Fact]
    public async Task HouseholdService_InviteCode_ShouldGenerateValidCodeWithoutAmbiguousChars()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var ownerId = "owner-1";
        var household = await service.CreateHouseholdAsync(ownerId, new CreateHouseholdDto("Gia đình Alpha"));

        var invite = await service.GenerateInviteCodeAsync(household.Id, ownerId);

        invite.InviteCode.Should().HaveLength(6);
        // Ensure no ambiguous characters: 0, O, 1, I
        invite.InviteCode.Should().NotContainAny("0", "O", "1", "I");
        invite.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddHours(24));

        var dbHousehold = await context.Households.FindAsync(household.Id);
        dbHousehold!.InviteCode.Should().Be(invite.InviteCode);
        dbHousehold.InviteCodeExpiresAt.Should().Be(invite.ExpiresAt);
    }

    [Fact]
    public async Task HouseholdService_InviteCode_NonAdminOrOwner_ShouldThrowUnauthorized()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var ownerId = "owner-1";
        var viewerId = "viewer-1";
        var household = await service.CreateHouseholdAsync(ownerId, new CreateHouseholdDto("Gia đình Beta"));

        // Add viewer
        context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = viewerId,
            Role = HouseholdRole.Viewer
        });
        await context.SaveChangesAsync();

        var act = () => service.GenerateInviteCodeAsync(household.Id, viewerId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task HouseholdService_JoinHouseholdByCode_ShouldSucceedWithValidCode()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var ownerId = "owner-1";
        var memberId = "member-new";
        var household = await service.CreateHouseholdAsync(ownerId, new CreateHouseholdDto("Gia đình Gamma"));
        var invite = await service.GenerateInviteCodeAsync(household.Id, ownerId);

        var joinResult = await service.JoinHouseholdByCodeAsync(memberId, invite.InviteCode);
        joinResult.Should().BeTrue();

        var updatedHousehold = await service.GetHouseholdByIdAsync(household.Id, memberId);
        updatedHousehold.Should().NotBeNull();
        updatedHousehold!.Members.Should().Contain(m => m.UserId == memberId && m.Role == HouseholdRole.Member);
    }

    [Fact]
    public async Task HouseholdService_JoinHouseholdByCode_ExpiredCode_ShouldFail()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var ownerId = "owner-1";
        var memberId = "member-new";
        var household = await service.CreateHouseholdAsync(ownerId, new CreateHouseholdDto("Gia đình Delta"));
        var invite = await service.GenerateInviteCodeAsync(household.Id, ownerId);

        // Manually expire code
        var dbHousehold = await context.Households.FindAsync(household.Id);
        dbHousehold!.InviteCodeExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await context.SaveChangesAsync();

        var joinResult = await service.JoinHouseholdByCodeAsync(memberId, invite.InviteCode);
        joinResult.Should().BeFalse();
    }

    [Theory]
    [InlineData("WRONG1")]
    [InlineData("NON_EXIST")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HouseholdService_JoinHouseholdByCode_InvalidCode_ShouldReturnFalse(string invalidCode)
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var ownerId = "owner-x";
        var memberId = "member-y";
        await service.CreateHouseholdAsync(ownerId, new CreateHouseholdDto("Gia đình Test Invalid"));

        var joinResult = await service.JoinHouseholdByCodeAsync(memberId, invalidCode);
        joinResult.Should().BeFalse();
    }

    [Fact]
    public async Task HouseholdService_RBAC_UpdateMemberRole_ShouldEnforcePermissions()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var ownerId = "owner-main";
        var adminId = "admin-user";
        var memberId = "regular-user";

        var household = await service.CreateHouseholdAsync(ownerId, new CreateHouseholdDto("Gia đình Epsilon"));
        context.HouseholdMembers.Add(new HouseholdMember { Id = Guid.NewGuid(), HouseholdId = household.Id, UserId = adminId, Role = HouseholdRole.Admin });
        context.HouseholdMembers.Add(new HouseholdMember { Id = Guid.NewGuid(), HouseholdId = household.Id, UserId = memberId, Role = HouseholdRole.Member });
        await context.SaveChangesAsync();

        // 1. Owner updates member to Viewer -> success
        var res1 = await service.UpdateMemberRoleAsync(household.Id, memberId, HouseholdRole.Viewer, ownerId);
        res1.Should().BeTrue();

        // 2. Admin tries to demote Owner -> fails with UnauthorizedAccessException
        var actDemoteOwner = () => service.UpdateMemberRoleAsync(household.Id, ownerId, HouseholdRole.Member, adminId);
        await actDemoteOwner.Should().ThrowAsync<UnauthorizedAccessException>();

        // 3. Admin tries to promote Member to Owner -> fails with UnauthorizedAccessException
        var actPromoteOwner = () => service.UpdateMemberRoleAsync(household.Id, memberId, HouseholdRole.Owner, adminId);
        await actPromoteOwner.Should().ThrowAsync<UnauthorizedAccessException>();

        // 4. Member tries to update roles -> fails
        var actMemberUpdate = () => service.UpdateMemberRoleAsync(household.Id, adminId, HouseholdRole.Viewer, memberId);
        await actMemberUpdate.Should().ThrowAsync<UnauthorizedAccessException>();

        // 5. Outsider tries to update roles -> fails
        var actOutsider = () => service.UpdateMemberRoleAsync(household.Id, memberId, HouseholdRole.Admin, "stranger");
        await actOutsider.Should().ThrowAsync<UnauthorizedAccessException>();

        // 6. Target member does not exist in household -> returns false
        var resNotFound = await service.UpdateMemberRoleAsync(household.Id, "unknown-user", HouseholdRole.Admin, ownerId);
        resNotFound.Should().BeFalse();
    }

    [Fact]
    public async Task HouseholdService_GetSharedWallets_ShouldReturnOnlySharedAccounts()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var userId = "user-abc";
        var household = await service.CreateHouseholdAsync(userId, new CreateHouseholdDto("Gia đình Zeta"));

        // Add a private account (IsShared = false)
        context.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            Name = "Ví cá nhân bí mật",
            Type = AccountType.Cash,
            Balance = 100000,
            IsShared = false
        });
        await context.SaveChangesAsync();

        var sharedWallets = (await service.GetSharedWalletsAsync(household.Id, userId)).ToList();

        sharedWallets.Should().HaveCount(1);
        sharedWallets.First().Name.Should().Contain("Gia đình Zeta");
    }

    [Fact]
    public async Task HouseholdService_GetSharedWallets_NonMember_ShouldThrowUnauthorized()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var ownerId = "owner-wallet";
        var outsiderId = "outsider-wallet";
        var household = await service.CreateHouseholdAsync(ownerId, new CreateHouseholdDto("Gia đình Shared Check"));

        var act = () => service.GetSharedWalletsAsync(household.Id, outsiderId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region Telegram Bot Service Tests

    [Fact]
    public async Task TelegramBotService_StartAndHelpCommands_ShouldSucceed()
    {
        using var context = CreateInMemoryDbContext();
        var stateMachine = new TransactionStateMachine();
        var aiParser = new AiVoiceExpenseParser();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var bot = new TelegramBotService(context, aiParser, stateMachine, config);

        var startUpdate = new TelegramUpdateDto
        {
            UpdateId = 1,
            Message = new TelegramMessageDto
            {
                MessageId = 100,
                Chat = new TelegramChatDto { Id = 12345 },
                Text = "/start"
            }
        };

        var helpUpdate = new TelegramUpdateDto
        {
            UpdateId = 2,
            Message = new TelegramMessageDto
            {
                MessageId = 101,
                Chat = new TelegramChatDto { Id = 12345 },
                Text = "/help"
            }
        };

        var startRes = await bot.HandleUpdateAsync(startUpdate);
        var helpRes = await bot.HandleUpdateAsync(helpUpdate);

        startRes.Should().BeTrue();
        helpRes.Should().BeTrue();
    }

    [Fact]
    public async Task TelegramBotService_ExpenseTextMessage_ShouldCreateDraftTransaction()
    {
        using var context = CreateInMemoryDbContext();
        var stateMachine = new TransactionStateMachine();
        var aiParser = new AiVoiceExpenseParser();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var bot = new TelegramBotService(context, aiParser, stateMachine, config);

        var update = new TelegramUpdateDto
        {
            UpdateId = 3,
            Message = new TelegramMessageDto
            {
                MessageId = 102,
                From = new TelegramUserDto { Id = 98765, FirstName = "Nguyen" },
                Chat = new TelegramChatDto { Id = 98765 },
                Text = "Ăn phở gà 65k tiền mặt hôm nay"
            }
        };

        var result = await bot.HandleUpdateAsync(update);
        result.Should().BeTrue();

        var transactions = await context.Transactions.ToListAsync();
        transactions.Should().HaveCount(1);

        var tx = transactions.First();
        tx.Amount.Should().Be(65000m);
        tx.Status.Should().Be(TransactionStatus.Draft); // Must be Draft
        tx.CreatedByUserId.Should().Be("tg_98765");
        tx.Fingerprint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TelegramBotService_CallbackQuery_Confirm_ShouldTransitionDraftToPending()
    {
        using var context = CreateInMemoryDbContext();
        var stateMachine = new TransactionStateMachine();
        var aiParser = new AiVoiceExpenseParser();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var bot = new TelegramBotService(context, aiParser, stateMachine, config);

        // Pre-create draft transaction
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = 50000,
            Date = DateTime.UtcNow,
            Status = TransactionStatus.Draft,
            Note = "Cà phê chiều",
            Fingerprint = "FP123"
        };
        context.Transactions.Add(tx);
        await context.SaveChangesAsync();

        var callbackUpdate = new TelegramUpdateDto
        {
            UpdateId = 4,
            CallbackQuery = new TelegramCallbackQueryDto
            {
                Id = "cq_1",
                From = new TelegramUserDto { Id = 98765, FirstName = "Nguyen" },
                Data = $"tx_confirm_{tx.Id}"
            }
        };

        var result = await bot.HandleUpdateAsync(callbackUpdate);
        result.Should().BeTrue();

        var updatedTx = await context.Transactions.FindAsync(tx.Id);
        updatedTx.Should().NotBeNull();
        updatedTx!.Status.Should().Be(TransactionStatus.Pending); // Successfully transitioned to Pending!
    }

    [Fact]
    public async Task TelegramBotService_CallbackQuery_Confirm_IdempotentOnNonDraft_ShouldNotReTransition()
    {
        using var context = CreateInMemoryDbContext();
        var stateMachine = new TransactionStateMachine();
        var aiParser = new AiVoiceExpenseParser();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var bot = new TelegramBotService(context, aiParser, stateMachine, config);

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = 50000,
            Date = DateTime.UtcNow,
            Status = TransactionStatus.Pending, // Already pending
            Note = "Cà phê sáng",
            Fingerprint = "FP456"
        };
        context.Transactions.Add(tx);
        await context.SaveChangesAsync();

        var callbackUpdate = new TelegramUpdateDto
        {
            UpdateId = 41,
            CallbackQuery = new TelegramCallbackQueryDto
            {
                Id = "cq_already_pending",
                From = new TelegramUserDto { Id = 98765 },
                Data = $"tx_confirm_{tx.Id}"
            }
        };

        var result = await bot.HandleUpdateAsync(callbackUpdate);
        result.Should().BeTrue();

        var checkTx = await context.Transactions.FindAsync(tx.Id);
        checkTx!.Status.Should().Be(TransactionStatus.Pending);
    }

    [Fact]
    public async Task TelegramBotService_CallbackQuery_Cancel_ShouldDeleteDraftTransaction()
    {
        using var context = CreateInMemoryDbContext();
        var stateMachine = new TransactionStateMachine();
        var aiParser = new AiVoiceExpenseParser();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var bot = new TelegramBotService(context, aiParser, stateMachine, config);

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = 50000,
            Date = DateTime.UtcNow,
            Status = TransactionStatus.Draft,
            Note = "Giao dịch nhầm",
            Fingerprint = "FP999"
        };
        context.Transactions.Add(tx);
        await context.SaveChangesAsync();

        var callbackUpdate = new TelegramUpdateDto
        {
            UpdateId = 5,
            CallbackQuery = new TelegramCallbackQueryDto
            {
                Id = "cq_2",
                From = new TelegramUserDto { Id = 98765, FirstName = "Nguyen" },
                Data = $"tx_cancel_{tx.Id}"
            }
        };

        var result = await bot.HandleUpdateAsync(callbackUpdate);
        result.Should().BeTrue();

        var deletedTx = await context.Transactions.FindAsync(tx.Id);
        deletedTx.Should().BeNull(); // Successfully deleted
    }

    [Fact]
    public async Task TelegramBotService_LinkCommand_ValidAndInvalidCodes_ShouldHandleCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var stateMachine = new TransactionStateMachine();
        var aiParser = new AiVoiceExpenseParser();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var bot = new TelegramBotService(context, aiParser, stateMachine, config);

        // Seed household with invite code
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Gia đình Bot Link",
            InviteCode = "ABCXYZ",
            InviteCodeExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        context.Households.Add(household);
        await context.SaveChangesAsync();

        // 1. Valid invite code
        var validUpdate = new TelegramUpdateDto
        {
            UpdateId = 10,
            Message = new TelegramMessageDto
            {
                MessageId = 200,
                From = new TelegramUserDto { Id = 55555, FirstName = "LinkUser" },
                Chat = new TelegramChatDto { Id = 55555 },
                Text = "/link ABCXYZ"
            }
        };

        var resValid = await bot.HandleUpdateAsync(validUpdate);
        resValid.Should().BeTrue();

        var members = await context.HouseholdMembers.Where(m => m.HouseholdId == household.Id).ToListAsync();
        members.Should().ContainSingle(m => m.UserId == "tg_55555" && m.Role == HouseholdRole.Member);

        // 2. Expired invite code
        household.InviteCodeExpiresAt = DateTime.UtcNow.AddHours(-1);
        await context.SaveChangesAsync();

        var expiredUpdate = new TelegramUpdateDto
        {
            UpdateId = 11,
            Message = new TelegramMessageDto
            {
                MessageId = 201,
                From = new TelegramUserDto { Id = 66666 },
                Chat = new TelegramChatDto { Id = 66666 },
                Text = "/link ABCXYZ"
            }
        };
        var resExpired = await bot.HandleUpdateAsync(expiredUpdate);
        resExpired.Should().BeTrue(); // Bot responds with error text
        var expiredMember = await context.HouseholdMembers.FirstOrDefaultAsync(m => m.UserId == "tg_66666");
        expiredMember.Should().BeNull(); // Not added

        // 3. Invalid invite code
        var invalidUpdate = new TelegramUpdateDto
        {
            UpdateId = 12,
            Message = new TelegramMessageDto
            {
                MessageId = 202,
                From = new TelegramUserDto { Id = 77777 },
                Chat = new TelegramChatDto { Id = 77777 },
                Text = "/link WRONGCODE"
            }
        };
        var resInvalid = await bot.HandleUpdateAsync(invalidUpdate);
        resInvalid.Should().BeTrue();
        var invalidMember = await context.HouseholdMembers.FirstOrDefaultAsync(m => m.UserId == "tg_77777");
        invalidMember.Should().BeNull(); // Not added
    }

    [Fact]
    public async Task TelegramBotService_VoiceMessage_ShouldCreateDraftTransaction()
    {
        using var context = CreateInMemoryDbContext();
        var stateMachine = new TransactionStateMachine();
        var aiParser = new AiVoiceExpenseParser();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var bot = new TelegramBotService(context, aiParser, stateMachine, config);

        var voiceUpdate = new TelegramUpdateDto
        {
            UpdateId = 20,
            Message = new TelegramMessageDto
            {
                MessageId = 300,
                From = new TelegramUserDto { Id = 88888, FirstName = "VoiceUser" },
                Chat = new TelegramChatDto { Id = 88888 },
                Voice = new TelegramVoiceDto
                {
                    FileId = "voice_file_123",
                    Duration = 5,
                    MimeType = "audio/ogg"
                }
            }
        };

        var result = await bot.HandleUpdateAsync(voiceUpdate);
        result.Should().BeTrue();

        var transactions = await context.Transactions.ToListAsync();
        transactions.Should().HaveCount(1);
        transactions.First().Status.Should().Be(TransactionStatus.Draft);
        transactions.First().CreatedByUserId.Should().Be("tg_88888");
    }

    #endregion

    #region WebApi Controllers Tests

    [Fact]
    public async Task TelegramWebhookController_ValidWebhook_ShouldReturnOk()
    {
        using var context = CreateInMemoryDbContext();
        var stateMachine = new TransactionStateMachine();
        var aiParser = new AiVoiceExpenseParser();
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Telegram:SecretToken"] = "my-secret-123",
            ["Telegram:BotToken"] = "mock-bot-token"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var bot = new TelegramBotService(context, aiParser, stateMachine, config);
        var controller = new TelegramWebhookController(bot, config, NullLogger<TelegramWebhookController>.Instance);

        var update = new TelegramUpdateDto
        {
            UpdateId = 1,
            Message = new TelegramMessageDto
            {
                MessageId = 1,
                Chat = new TelegramChatDto { Id = 123 },
                Text = "/help"
            }
        };

        // 1. With correct secret token
        var okResult = await controller.Webhook(update, "my-secret-123", default);
        okResult.Should().BeOfType<OkObjectResult>();

        // 2. With invalid secret token
        var unauthResult = await controller.Webhook(update, "wrong-token", default);
        unauthResult.Should().BeOfType<UnauthorizedObjectResult>();

        // 3. Status endpoint
        var statusResult = controller.GetStatus() as OkObjectResult;
        statusResult.Should().NotBeNull();
    }

    [Theory]
    [InlineData("wrong-secret")]
    [InlineData("")]
    [InlineData(null)]
    public async Task TelegramWebhookController_SecretTokenMismatchOrMissing_ShouldReturnUnauthorized(string? secretToken)
    {
        using var context = CreateInMemoryDbContext();
        var stateMachine = new TransactionStateMachine();
        var aiParser = new AiVoiceExpenseParser();
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Telegram:SecretToken"] = "secure-secret-phase4-999",
            ["Telegram:BotToken"] = "mock-bot-token"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var bot = new TelegramBotService(context, aiParser, stateMachine, config);
        var controller = new TelegramWebhookController(bot, config, NullLogger<TelegramWebhookController>.Instance);

        var update = new TelegramUpdateDto
        {
            UpdateId = 101,
            Message = new TelegramMessageDto
            {
                MessageId = 2,
                Chat = new TelegramChatDto { Id = 123 },
                Text = "Cơm trưa 50k"
            }
        };

        var result = await controller.Webhook(update, secretToken, default);
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task HouseholdController_CrudAndInviteFlow_ShouldSucceed()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);
        var controller = new HouseholdController(service);

        // 1. Create Household
        var createResult = await controller.Create(new CreateHouseholdDto("Gia đình Hạnh Phúc")) as CreatedAtActionResult;
        createResult.Should().NotBeNull();
        var householdDto = createResult!.Value as HouseholdDetailDto;
        householdDto.Should().NotBeNull();

        // 2. Get My Households
        var myResult = await controller.GetMyHouseholds() as OkObjectResult;
        myResult.Should().NotBeNull();
        var myHouseholds = myResult!.Value as IEnumerable<HouseholdDetailDto>;
        myHouseholds.Should().ContainSingle(h => h.Id == householdDto!.Id);

        // 3. Generate Invite Code
        var inviteResult = await controller.GenerateInviteCode(householdDto!.Id) as OkObjectResult;
        inviteResult.Should().NotBeNull();
        var inviteDto = inviteResult!.Value as GenerateInviteCodeResultDto;
        inviteDto.Should().NotBeNull();
        inviteDto!.InviteCode.Should().HaveLength(6);

        // 4. Get Shared Wallets
        var walletsResult = await controller.GetSharedWallets(householdDto.Id) as OkObjectResult;
        walletsResult.Should().NotBeNull();
        var wallets = walletsResult!.Value as IEnumerable<AccountDto>;
        wallets.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HouseholdController_Join_InvalidOrExpiredCode_ShouldReturnBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);
        var controller = new HouseholdController(service);

        // 1. Non-existent code
        var badCodeResult = await controller.Join(new JoinHouseholdDto("NONEXISTENT"));
        badCodeResult.Should().BeOfType<BadRequestObjectResult>();

        // 2. Empty code
        var emptyCodeResult = await controller.Join(new JoinHouseholdDto(""));
        emptyCodeResult.Should().BeOfType<BadRequestObjectResult>();

        // 3. Expired code
        var ownerHousehold = await service.CreateHouseholdAsync("owner-user", new CreateHouseholdDto("Gia đình Test Expire"));
        var invite = await service.GenerateInviteCodeAsync(ownerHousehold.Id, "owner-user");

        var dbHousehold = await context.Households.FindAsync(ownerHousehold.Id);
        dbHousehold!.InviteCodeExpiresAt = DateTime.UtcNow.AddMinutes(-10);
        await context.SaveChangesAsync();

        var expiredResult = await controller.Join(new JoinHouseholdDto(invite.InviteCode));
        expiredResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HouseholdController_RBAC_UnauthorizedOperations_ShouldReturnForbidden403()
    {
        using var context = CreateInMemoryDbContext();
        var service = new HouseholdService(context);

        var ownerId = "owner-admin";
        var regularMemberId = "member-regular";
        var outsiderId = "outsider-user";

        var household = await service.CreateHouseholdAsync(ownerId, new CreateHouseholdDto("Gia đình Phân Quyền"));
        context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = regularMemberId,
            Role = HouseholdRole.Member
        });
        await context.SaveChangesAsync();

        // 1. Regular Member tries to GenerateInviteCode -> 403 Forbidden
        var memberController = CreateHouseholdControllerWithUser(service, regularMemberId);
        var genInviteResult = await memberController.GenerateInviteCode(household.Id) as ObjectResult;
        genInviteResult.Should().NotBeNull();
        genInviteResult!.StatusCode.Should().Be(403);

        // 2. Regular Member tries to UpdateMemberRole -> 403 Forbidden
        var updateRoleResult = await memberController.UpdateMemberRole(
            household.Id, 
            ownerId, 
            new UpdateMemberRoleDto(HouseholdRole.Viewer)) as ObjectResult;
        updateRoleResult.Should().NotBeNull();
        updateRoleResult!.StatusCode.Should().Be(403);

        // 3. Outsider tries to GetSharedWallets -> 403 Forbidden
        var outsiderController = CreateHouseholdControllerWithUser(service, outsiderId);
        var getWalletsResult = await outsiderController.GetSharedWallets(household.Id) as ObjectResult;
        getWalletsResult.Should().NotBeNull();
        getWalletsResult!.StatusCode.Should().Be(403);

        // 4. Outsider tries to GetById -> 404 NotFound (or access denied)
        var getByIdResult = await outsiderController.GetById(household.Id) as ObjectResult;
        getByIdResult.Should().NotBeNull();
        getByIdResult!.StatusCode.Should().Be(404);
    }

    #endregion
}
