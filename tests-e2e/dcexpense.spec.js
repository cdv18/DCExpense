// @ts-check
const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { resetReconciliationData } = require('./dbReset');

// Ensure screenshots folder exists
const screenshotDir = path.join(__dirname, 'screenshots');
if (!fs.existsSync(screenshotDir)) {
    fs.mkdirSync(screenshotDir, { recursive: true });
}

/**
 * Helper to ensure the page is authenticated as Admin.
 */
async function ensureLoggedIn(page) {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const modal = page.locator('#loginModal');
    if (await modal.isVisible()) {
        await page.locator('#btnQuickLogin').click();
        await expect(modal).toBeHidden({ timeout: 8000 });
        await expect(page.locator('#userFullName')).toContainText('Triệu Quang Quân', { timeout: 8000 });
    }
}

test.describe('DCExpense End-to-End (E2E) Test Suite', () => {

    test.beforeAll(() => {
        // Reset DB reconciliation candidates before test run
        resetReconciliationData();
    });

    test.beforeEach(async ({ page }) => {
        page.on('console', msg => console.log('PAGE LOG:', msg.text()));
        page.on('pageerror', err => console.log('PAGE ERROR:', err.message));
    });

    test('Test 1: Authentication & SSO Flow (DCManager)', async ({ page }) => {
        // 1. Navigate to DCExpense Web SPA
        await page.goto('/');
        await page.waitForLoadState('networkidle');

        // 2. Verify Login Modal is displayed on initial visit
        const loginModal = page.locator('#loginModal');
        await expect(loginModal).toBeVisible();
        await expect(loginModal).toContainText('Đăng Nhập DCManager');
        await expect(page.locator('#btnQuickLogin')).toBeVisible();

        // 3. Click Quick Demo Login button
        await page.locator('#btnQuickLogin').click();

        // 4. Verify Welcome Toast is displayed and modal closes
        await expect(loginModal).toBeHidden({ timeout: 8000 });
        const toast = page.locator('#toast');
        await expect(toast).toBeVisible();
        await expect(page.locator('#toastMessage')).toContainText(/Chào mừng|thành công/i);

        // 5. Verify Header displays authenticated user information
        const authContainer = page.locator('#authAuthenticated');
        await expect(authContainer).toBeVisible();
        await expect(page.locator('#userFullName')).toHaveText('Triệu Quang Quân');
        await expect(page.locator('#userEmail')).toHaveText('admin@dcmanager.local');
        await expect(page.locator('#btnLogout')).toBeVisible();

        // Capture screenshot
        await page.screenshot({ path: path.join(screenshotDir, 'test1_auth_sso_success.png'), fullPage: true });
    });

    test('Test 2: Real Database Data Binding (Dashboard, Wallets, Budgets, Chart)', async ({ page }) => {
        await ensureLoggedIn(page);

        // 1. Verify Net Worth displays real database value (around 135M VND)
        const netWorthEl = page.locator('#netWorthDisplay');
        await expect(netWorthEl).toBeVisible();
        await expect(netWorthEl).not.toHaveText('-- đ', { timeout: 8000 });
        const netWorthText = await netWorthEl.textContent();
        expect(netWorthText).toMatch(/13[45]\.\d{3}\.\d{3}\s*đ/);

        // Verify Assets & Liabilities
        const assetsEl = page.locator('#assetsDisplay');
        const liabilitiesEl = page.locator('#liabilitiesDisplay');
        await expect(assetsEl).not.toHaveText('--');
        await expect(liabilitiesEl).not.toHaveText('--');

        // 2. Verify all 4 real Wallets/Accounts rendered from DB
        const walletsContainer = page.locator('#walletsContainer');
        await expect(walletsContainer).toBeVisible();
        await expect(walletsContainer).toContainText('Ví Tiền Mặt');
        await expect(walletsContainer).toContainText('Tài khoản Vietcombank');
        await expect(walletsContainer).toContainText('Ví MoMo');
        await expect(walletsContainer).toContainText('Thẻ tín dụng Techcombank');

        // 3. Verify Budgets Progress (50/30/20 & JARS) displays dynamic data
        const budgetsContainer = page.locator('#budgetsContainer');
        await expect(budgetsContainer).toBeVisible();
        await expect(budgetsContainer).toContainText('Nhu cầu thiết yếu');
        await expect(budgetsContainer).toContainText('Mong muốn');

        // 4. Verify Chart.js Canvas is rendered and visible
        const cashFlowChart = page.locator('#cashFlowChart');
        await expect(cashFlowChart).toBeVisible();

        // Capture screenshot
        await page.screenshot({ path: path.join(screenshotDir, 'test2_database_data_binding.png'), fullPage: true });
    });

    test('Test 3: Real Transaction Creation & Immediate State Update', async ({ page }) => {
        resetReconciliationData();
        await ensureLoggedIn(page);

        // 1. Open New Transaction Modal
        const btnOpenTxModal = page.locator('#btnOpenTxModal');
        await expect(btnOpenTxModal).toBeVisible();
        await btnOpenTxModal.click();

        const txModal = page.locator('#txModal');
        await expect(txModal).toBeVisible();

        // 2. Verify Wallets and Categories dropdowns loaded dynamic data
        const accountSelect = page.locator('#txAccountId');
        const categorySelect = page.locator('#txCategoryId');
        await expect(accountSelect.locator('option', { hasText: 'MoMo' })).toBeAttached({ timeout: 8000 });
        await expect(categorySelect.locator('option', { hasText: 'Ăn uống' })).toBeAttached({ timeout: 8000 });

        // 3. Fill transaction data
        // Select Wallet: MoMo
        const momoOption = accountSelect.locator('option', { hasText: 'MoMo' });
        const momoValue = await momoOption.getAttribute('value');
        if (momoValue) {
            await accountSelect.selectOption(momoValue);
        }

        // Select Category: Ăn uống
        const foodOption = categorySelect.locator('option', { hasText: 'Ăn uống' });
        const foodValue = await foodOption.getAttribute('value');
        if (foodValue) {
            await categorySelect.selectOption(foodValue);
        }

        // Fill Amount & Note
        const testNote = 'Phở sáng Playwright E2E';
        await page.locator('#txAmount').fill('65000');
        await page.locator('#txNote').fill(testNote);

        // Verify total calculation
        await expect(page.locator('#txTotal')).toHaveText('65.000');

        // 4. Submit form
        await page.locator('#btnSaveTransaction').click();

        // 5. Verify Toast notification and Modal closure
        await expect(txModal).toBeHidden({ timeout: 5000 });
        const toast = page.locator('#toast');
        await expect(toast).toBeVisible();
        await expect(page.locator('#toastMessage')).toContainText(/lưu|thành công/i);

        // 6. Verify new transaction appears in Recent Transactions list
        const recentTbody = page.locator('#recentTransactionsTableBody');
        await expect(recentTbody).toContainText(testNote, { timeout: 8000 });
        await expect(recentTbody).toContainText('-65.000');

        // Capture screenshot
        await page.screenshot({ path: path.join(screenshotDir, 'test3_transaction_creation.png'), fullPage: true });
    });

    test('Test 4: Real Bank Statement Reconciliation (Staging UI & 1-Touch Merge)', async ({ page }) => {
        // Reset reconciliation data to guarantee fresh Pending candidates
        resetReconciliationData();

        await ensureLoggedIn(page);

        // 1. Switch to Reconciliation view
        const navReconciliation = page.locator('#nav-reconciliation');
        await navReconciliation.click();
        await expect(page.locator('#view-reconciliation')).toHaveClass(/active/);

        // 2. Click "Thử nghiệm ngay với file mẫu"
        const btnSample = page.locator('button', { hasText: 'Thử nghiệm ngay với file mẫu' });
        await expect(btnSample).toBeVisible();
        await btnSample.click();

        // 3. Verify Staging Tables are displayed
        const stagingTables = page.locator('#reconciliationTables');
        await expect(stagingTables).toBeVisible({ timeout: 10000 });

        // 4. Verify 3 Classification Groups:
        // Group 1: Auto-merged (Khớp tự động: Tiền điện 1.2M)
        await expect(page.locator('#autoMergedCountBadge')).toContainText(/1 Giao dịch/i);
        const autoMergedTbody = page.locator('#autoMergedTbody');
        await expect(autoMergedTbody).toContainText('Thanh toán tiền điện');
        await expect(autoMergedTbody).toContainText('1.200.000');
        await expect(autoMergedTbody).toContainText('Khớp');

        // Group 2: Suspect (Nghi vấn lệch ngày: Shopee 450K)
        await expect(page.locator('#suspectCountBadge')).toContainText(/1 Cặp/i);
        const suspectContainer = page.locator('#suspectContainer');
        await expect(suspectContainer).toContainText('THANH TOAN SHOPEE');
        await expect(suspectContainer).toContainText('450.000');
        const btnMerge = suspectContainer.locator('button', { hasText: 'Gộp 1-chạm' });
        await expect(btnMerge).toBeVisible();

        // Group 3: New Transactions (Cafe, Nhà hàng)
        await expect(page.locator('#newTxCountBadge')).toContainText(/2 Giao dịch/i);
        const newTxTbody = page.locator('#newTxTbody');
        await expect(newTxTbody).toContainText('CHUYEN TIEN CAFE');
        await expect(newTxTbody).toContainText('NHA HANG PHUONG DONG');

        // 5. Test "Gộp 1-chạm" on suspect transaction
        await btnMerge.click();

        // Verify merge success feedback in suspect card and toast
        await expect(suspectContainer).toContainText(/Đã gộp thành công/i, { timeout: 8000 });
        const toast = page.locator('#toast');
        await expect(toast).toBeVisible();
        await expect(page.locator('#toastMessage')).toContainText(/thành công/i);

        // Capture screenshot
        await page.screenshot({ path: path.join(screenshotDir, 'test4_bank_reconciliation.png'), fullPage: true });
    });

    test('Test 5: Theme Toggle (Dark/Light mode)', async ({ page }) => {
        await ensureLoggedIn(page);

        const htmlEl = page.locator('html');
        const themeBtn = page.locator('#themeToggle');
        await expect(themeBtn).toBeVisible();

        // Get initial theme
        const isInitiallyDark = await htmlEl.evaluate(el => el.classList.contains('dark'));

        // Toggle theme once
        await themeBtn.click();
        if (isInitiallyDark) {
            await expect(htmlEl).not.toHaveClass(/dark/);
        } else {
            await expect(htmlEl).toHaveClass(/dark/);
        }

        // Toggle theme back
        await themeBtn.click();
        if (isInitiallyDark) {
            await expect(htmlEl).toHaveClass(/dark/);
        } else {
            await expect(htmlEl).not.toHaveClass(/dark/);
        }

        // Capture screenshot
        await page.screenshot({ path: path.join(screenshotDir, 'test5_theme_toggle.png'), fullPage: true });
    });

});
