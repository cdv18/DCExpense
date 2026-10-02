using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using DCExpense.Application.Interfaces;

namespace DCExpense.Infrastructure.Services;

public class BankStatementParser : IBankStatementParser
{
    public IEnumerable<ImportedTransaction> ParseCsv(Stream csvStream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null
        };

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, config);
        
        // Registering a flexible map to handle different bank formats
        csv.Context.RegisterClassMap<BankTransactionMap>();
        
        var records = csv.GetRecords<ImportedTransaction>();
        
        var results = new List<ImportedTransaction>();
        foreach (var r in records)
        {
            results.Add(r);
        }
        return results;
    }

    private sealed class BankTransactionMap : ClassMap<ImportedTransaction>
    {
        public BankTransactionMap()
        {
            // Map common column names from banks like Vietcombank, Techcombank, VPBank, MB
            Map(m => m.Date).Name("Date", "Ngày", "Ngày giao dịch", "Ngay giao dich", "Transaction Date").Optional();
            Map(m => m.Amount).Name("Amount", "Số tiền", "So tien", "Credit", "Debit", "Giá trị").Optional();
            Map(m => m.BankRefId).Name("Transaction ID", "Mã giao dịch", "Ma giao dich", "Ref No", "Số bút toán").Optional();
            Map(m => m.Description).Name("Description", "Nội dung", "Noi dung", "Chi tiết", "Details").Optional();
        }
    }
}
