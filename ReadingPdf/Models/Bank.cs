namespace ReadingPdf.Models
{
    public class Bank
    {
        public int BankId { get; set; }
        public string BankName { get; set; }    
        public string BankAccountNumber { get; set; }

        public string BankIdentifier { get; set; }

        public string DateFormat { get; set; }

        // Navigation property: un Bank puede tener varias BankAccounts
        public System.Collections.Generic.ICollection<BankAccount> BankAccounts { get; set; } = new System.Collections.Generic.List<BankAccount>();
    }
}
