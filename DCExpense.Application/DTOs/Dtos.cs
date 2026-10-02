using System;
using System.Collections.Generic;
using DCExpense.Domain.Enums;

namespace DCExpense.Application.DTOs;

public record HouseholdDto(Guid Id, string Name);
public record AccountDto(Guid Id, string Name, AccountType Type, decimal Balance, string Currency);
public record CategoryDto(Guid Id, string Name, Guid? ParentId, BudgetType DefaultBudgetType);
public record TransactionDto(Guid Id, Guid AccountId, Guid? CategoryId, decimal Amount, DateTime Date, string Note, TransactionType Type, TransactionStatus Status);
public record CreateSplitItemDto(decimal Amount, Guid? CategoryId, string Note);
public record CreateTransactionDto(
    Guid AccountId, 
    Guid? CategoryId, 
    decimal Amount, 
    DateTime Date, 
    string Note, 
    TransactionType Type,
    List<CreateSplitItemDto>? Splits = null,
    string? BankRefId = null,
    string? CreatedByUserId = null);
