using System;

namespace ReadingPdf.Models
{
    public class CreditCardChargeDto
    {
        public DateTime TxnDate { get; set; } = DateTime.UtcNow;
        public string PayeeFullName { get; set; } = "";
        public string AccountFullName { get; set; } = "";
        public string Memo { get; set; } = "";
        public string ExpenseAccountFullName { get; set; } = "";
        public double Amount { get; set; }
    }
}