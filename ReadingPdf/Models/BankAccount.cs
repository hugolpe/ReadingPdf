namespace ReadingPdf.Models
{
    public class BankAccount
    {
        public int BankAccId { get; set; }
        public string BankAccName { get; set; } = string.Empty;

        // Foreign key to Bank
        public int BankId { get; set; }
        public Bank? Bank { get; set; }
    }
}