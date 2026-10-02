namespace DCExpense.Domain.Enums;

public enum AccountType { Cash, Bank, CreditCard, Savings, Investment, EWallet }
public enum TransactionType { Income, Expense, Transfer, DebtPayment, Reimbursement }
public enum TransactionStatus { Draft, Pending, Cleared, Reconciled }
public enum HouseholdRole { Owner, Admin, Member, Viewer }
public enum BudgetType { Need, Want, Savings, Investment, None }
