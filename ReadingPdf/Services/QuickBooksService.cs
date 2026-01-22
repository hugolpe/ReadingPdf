using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using QBFC13Lib;
using ReadingPdf.Models;

namespace ReadingPdf.Services
{
    public class QuickBooksService : IQuickBooksService
    {
        public async Task<QuickBooksResult> CreateCreditCardChargeAsync(CreditCardChargeDto dto)
        {
            if (dto == null)
                return QuickBooksResult.Failure("dto is null");

            // Basic validation up-front so we don't call the SDK with empty required fields
            if (string.IsNullOrWhiteSpace(dto.AccountFullName))
                return QuickBooksResult.Failure("Credit card account (AccountFullName) is required.");

            if (string.IsNullOrWhiteSpace(dto.ExpenseAccountFullName))
                return QuickBooksResult.Failure("Expense account (ExpenseAccountFullName) is required.");

            var tcs = new TaskCompletionSource<QuickBooksResult>();

            var thread = new Thread(() =>
            {
                QBSessionManager sessionManager = null;
                IMsgSetRequest request = null;
                IMsgSetResponse response = null;

                try
                {
                    sessionManager = new QBSessionManager();

                    // Use OpenConnection2 and explicit connection type (more robust)
                    sessionManager.OpenConnection2("", "ReadingPdf QuickBooks CreditCardCharge", ENConnectionType.ctLocalQBD);
                    sessionManager.BeginSession("", ENOpenMode.omDontCare);

                    // Create request
                    request = sessionManager.CreateMsgSetRequest("US", 13, 0);
                    request.Attributes.OnError = ENRqOnError.roeStop;

                    // Append CreditCardChargeAdd request
                    var chargeRq = request.AppendCreditCardChargeAddRq();

                    // Required fields
                    chargeRq.TxnDate.SetValue(dto.TxnDate == default ? DateTime.UtcNow : dto.TxnDate);
                    if (!string.IsNullOrWhiteSpace(dto.PayeeFullName))
                        chargeRq.PayeeEntityRef.FullName.SetValue(dto.PayeeFullName);

                    // IMPORTANT: use CreditCardAccountRef for the credit card account (not AccountRef)
                    chargeRq.AccountRef.FullName.SetValue(dto.AccountFullName);

                    if (!string.IsNullOrWhiteSpace(dto.Memo))
                        chargeRq.Memo.SetValue(dto.Memo);

                    // Expense line
                    var expenseLine = chargeRq.ExpenseLineAddList.Append();
                    expenseLine.AccountRef.FullName.SetValue(dto.ExpenseAccountFullName);
                    expenseLine.Amount.SetValue(dto.Amount);
                    if (!string.IsNullOrWhiteSpace(dto.Memo))
                        expenseLine.Memo.SetValue(dto.Memo);

                    // Execute
                    response = sessionManager.DoRequests(request);

                    if (response == null || response.ResponseList == null || response.ResponseList.Count == 0)
                    {
                        tcs.SetResult(QuickBooksResult.Failure("No response from QuickBooks."));
                        return;
                    }

                    var resp = response.ResponseList.GetAt(0);
                    if (resp.StatusCode == 0)
                    {
                        var chargeRet = resp.Detail as ICreditCardChargeRet;
                        var txnId = chargeRet?.TxnID?.GetValue() ?? string.Empty;
                        tcs.SetResult(QuickBooksResult.Success(txnId));
                    }
                    else
                    {
                        // include status code and message for diagnostic
                        tcs.SetResult(QuickBooksResult.Failure($"{resp.StatusCode}: {resp.StatusMessage}"));
                    }
                }
                catch (COMException comEx)
                {
                    // REGDB_E_CLASSNOTREG = 0x80040154 -> componente COM no registrado
                    if ((uint)comEx.ErrorCode == 0x80040154u)
                    {
                        tcs.SetResult(QuickBooksResult.Failure("COM class not registered (0x80040154). Ensure QuickBooks Desktop and the QBFC runtime are installed on this machine and that the process bitness matches (x86)."));
                    }
                    else
                    {
                        tcs.SetResult(QuickBooksResult.Failure($"COM error: 0x{(uint)comEx.ErrorCode:X8} - {comEx.Message}"));
                    }
                }
                catch (Exception ex)
                {
                    // return full exception message for diagnostics
                    tcs.SetResult(QuickBooksResult.Failure(ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : "")));
                }
                finally
                {
                    // Best-effort cleanup; release COM objects in reverse creation order
                    try { sessionManager?.EndSession(); } catch { }
                    try { sessionManager?.CloseConnection(); } catch { }

                    if (response != null) Marshal.FinalReleaseComObject(response);
                    if (request != null) Marshal.FinalReleaseComObject(request);
                    if (sessionManager != null) Marshal.FinalReleaseComObject(sessionManager);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return await tcs.Task;
        }
    }

    public interface IQuickBooksService
    {
        Task<QuickBooksResult> CreateCreditCardChargeAsync(CreditCardChargeDto dto);
    }
}