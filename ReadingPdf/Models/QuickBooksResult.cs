namespace ReadingPdf.Models
{
    public class QuickBooksResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        public string TxnId { get; }

        private QuickBooksResult(bool isSuccess, string message, string txnId = null)
        {
            IsSuccess = isSuccess;
            Message = message;
            TxnId = txnId;
        }

        public static QuickBooksResult Success(string txnId) =>
            new QuickBooksResult(true, "Success", txnId);

        public static QuickBooksResult Failure(string message) =>
            new QuickBooksResult(false, message);
    }
}