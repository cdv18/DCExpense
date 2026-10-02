# 🧮 DCExpense - Smart Reconciliation & Deduplication Engine

## 1. Problem Statement: The Out-of-Order & Transposition Dilemma

In real-world personal and household finance, transactions rarely enter the ledger in chronological, pristine order:
- **Scenario**: 
  1. *Day 15*: User spends `530,000 VND` at a restaurant with credit card, but forgets to record it.
  2. *Day 20*: User remembers and hurriedly types on Telegram `350,000 VND` (human typo error transposing digits `53` into `35`).
  3. *Day 30*: User uploads the official monthly bank statement CSV containing the exact record: `530,000 VND` on *Day 15*.
- **Consequence on Traditional Apps**: 
  The app duplicates the entry, recording total spending of $530,000 + 350,000 = 880,000\text{ VND}$, catastrophically skewing monthly reports and budget thresholds!

---

## 2. DCExpense Multi-Layer Matching Architecture

To resolve this without forcing tedious manual reconciliation, DCExpense employs a three-tiered algorithmic pipeline in [`ReconciliationService.cs`](../DCExpense.Application/Services/ReconciliationService.cs):

```
       Bank Statement Record (CSV/Excel)
                     │
                     ▼
       ┌───────────────────────────┐
       │   Exact BankRefId Match   │ ──(Match)──> AUTO-MERGED (100% Confidence)
       └─────────────┬─────────────┘
                     │ (No Ref Match)
                     ▼
       ┌───────────────────────────┐
       │ Sliding Window (±7 Days)  │ ──(Out of Range >7d)──> NEW TRANSACTION
       └─────────────┬─────────────┘
                     │ (Within Window)
                     ▼
       ┌───────────────────────────┐
       │ Transposition Error Check │ ──(|A - B| mod 9 == 0)──> SUSPECT (Transposition Typo)
       │    (|A - B| mod 9 == 0)   │
       └─────────────┬─────────────┘
                     │
                     ▼
       ┌───────────────────────────┐
       │  Fuzzy Description Score  │ ──(Score ≥ 80%)──> AUTO-MERGED
       │     (Jaro-Winkler NLP)    │ ──(50% ≤ Score < 80%)──> SUSPECT (1-Click Merge)
       └───────────────────────────┘
```

---

## 3. Mathematical Foundations

### 3.1. Sliding-Window Temporal Tolerance ($\pm 7$ Days)
Bank processing days, weekends, holidays, and human delayed memory often cause timestamp divergence:
$$\Delta t = |\text{Date}_{\text{Bank}} - \text{Date}_{\text{Internal}}|$$
- If $\Delta t > 7\text{ days}$: The candidate is immediately excluded from matching, preventing false positives across monthly billing cycles.
- If $\Delta t \le 7\text{ days}$: Time penalty is linearly scaled:
  $$\text{Score}_{\text{Time}} = 1.0 - \left(\frac{\Delta t}{7}\right) \times 0.2$$

### 3.2. Transposition Error Theorem (Modulo 9 Checker)
A fundamental mathematical theorem of the base-10 positional numeral system states that **swapping any two digits in a number always yields a difference that is an exact multiple of 9**:
$$|A - B| \equiv 0 \pmod 9$$

*Proof*:
Let $A = \sum_{k=0}^{n} d_k 10^k$. Swapping digits at positions $i$ and $j$ ($i > j$) produces:
$$B = A - d_i 10^i - d_j 10^j + d_i 10^j + d_j 10^i = A + (d_i - d_j)(10^j - 10^i)$$
Since $10^m \equiv 1 \pmod 9$ for all integers $m \ge 0$, we have $10^j - 10^i \equiv 1 - 1 \equiv 0 \pmod 9$.
Thus, $A - B$ is divisible by 9. $\blacksquare$

In DCExpense:
```csharp
bool isTransposition = (amountDiff > 0) &&
                       (amountDiff % 9 == 0) &&
                       (statementAmount.ToString().Length == candidate.Amount.ToString().Length);
```
When this condition holds, the transaction is flagged with `SuspectReason = "Nghi vấn gõ nhầm đảo số (Transposition Typo)"` and presented on the Staging UI with a **1-Touch Merge** action button.

### 3.3. SHA-256 Anti-Tampering Fingerprint Idempotency
To guarantee that importing the same file or statement multiple times never duplicates transactions:
$$\text{Fingerprint} = \text{SHA256}\Big(\text{AccountId} \,\|\, \text{Date:yyyyMMdd} \,\|\, \text{Amount} \,\|\, \text{CleanNote}\Big)$$
A unique database constraint on `(AccountId, Fingerprint)` guarantees absolute idempotency at the storage layer.

---

## 4. Staging UI Classification

When a bank statement CSV is uploaded, records are bucketed into 3 intuitive groups:
1. **Auto-Merged ($\ge 80\%$ confidence)**: Automatically matched and synced. Status moves to `Cleared`.
2. **Suspect ($\ge 50\%$ and $< 80\%$ confidence)**: Highlighted with diff badges (Date diff, Amount transposition). User confirms with 1-click.
3. **New (< 50% confidence)**: Brand new external spendings not previously logged. Click to categorize and add to ledger.
