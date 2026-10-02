const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

function resetReconciliationData() {
    try {
        const psqlPath = 'C:\\Users\\noadv\\AppData\\Local\\Programs\\pgsql\\bin\\psql.exe';
        const tmpSql = path.join(__dirname, '_reset_tmp.sql');
        const sql = `
-- Clean up any previous test transactions
DELETE FROM dcexpense."Transactions" WHERE "Note" ILIKE '%Playwright E2E%';

-- Reset Bank Reconciliation candidate transactions to Pending status and original date/note
UPDATE dcexpense."Transactions" 
SET "Status" = 1, "BankRefId" = NULL, "Note" = 'Thanh toán tiền điện', "Date" = '2026-10-01 09:15:00+00' 
WHERE "Id" = '5e383878-d5c8-4b89-8ecf-5b004beeb061' OR ("Amount" = 1200000 AND "Note" ILIKE '%tiền điện%');

UPDATE dcexpense."Transactions" 
SET "Status" = 1, "BankRefId" = NULL, "Note" = 'Mua sắm Shopee', "Date" = '2026-10-02 11:30:00+00' 
WHERE "Id" = 'ae86bdf2-1b3e-46ea-8529-f4cc356d05f0' OR ("Amount" = 450000 AND "Note" ILIKE '%Shopee%');
`;
        fs.writeFileSync(tmpSql, sql, 'utf8');
        execSync(`"${psqlPath}" -h localhost -U postgres -d dcexpense_db -f "${tmpSql}"`, {
            env: { ...process.env, PGPASSWORD: 'postgres' },
            stdio: 'pipe'
        });
        if (fs.existsSync(tmpSql)) fs.unlinkSync(tmpSql);
        console.log('Database reconciliation test data reset successfully.');
    } catch (err) {
        console.warn('Warning resetting reconciliation DB data:', err.message);
    }
}

if (require.main === module) {
    resetReconciliationData();
}

module.exports = { resetReconciliationData };
