using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models = ReadingPdf.Models;
using ReadingPdf.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ReadingPdf.Models;
using Microsoft.AspNetCore.Http;

namespace ReadingPdf.Controllers
{
    [ApiController]
    [Route("api/quickbooks")]
    public class QuickBooksApiController : ControllerBase
    {
        private readonly IIIFBuilderService _iifBuilder;
        private readonly ILogger<QuickBooksApiController> _logger;
        private readonly IQuickBooksService _qb;
        private const string SelectedAccountCookieName = "qb_selected_account";

        public QuickBooksApiController(
            IIIFBuilderService iifBuilder,
            ILogger<QuickBooksApiController> logger,
            IQuickBooksService qb)
        {
            _iifBuilder = iifBuilder;
            _logger = logger;
            _qb = qb;
        }

        // DTO to persist selected account
        public class SelectAccountDto
        {
            public string AccountFullName { get; set; } = "";
        }

        [HttpPost("select-account")]
        public IActionResult SelectAccount([FromBody] SelectAccountDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.AccountFullName))
                return BadRequest(new { error = "AccountFullName is required" });

            var opts = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = false,
                Path = "/"
            };

            Response.Cookies.Append(SelectedAccountCookieName, dto.AccountFullName, opts);

            _logger?.LogInformation("QuickBooks: selected account saved in cookie: {Account}", dto.AccountFullName);
            return Ok(new { success = true, selected = dto.AccountFullName });
        }

        /* 
        PSEUDOCODE / PLAN:
        1. Validate incoming Models.CreditCardChargeDto (check null).
        2. Determine which account to use as the expense account:
            - If dto.ExpenseAccountFullName is provided, use it.
            - Otherwise use dto.AccountFullName (the selected account).
            Note: "AccountFullName es la cuenta seleccionada" -> use as fallback.
        3. Ensure TxnDate has a sensible default (use UTC now if default).
        4. Map fields into the service-facing CreditCardChargeDto, using the resolved expense account.
        5. Call _qb.CreateCreditCardChargeAsync(mappedDto) and return appropriate HTTP response.
        */

        [HttpPost("charge")]
        public async Task<IActionResult> Charge([FromBody] Models.CreditCardChargeDto dto)
        {
            if (dto == null)
                return BadRequest(new { error = "Payload inválido" });

            // --- Separa la "cuenta de tarjeta" (AccountFullName enviada en el payload)
            // de la "cuenta seleccionada/guardada" (cookie). No sobrescribimos la cookie
            // con la cuenta de la tarjeta porque eso provocaba que ExpenseAccount use la
            // misma cuenta de la tarjeta como fallback.
            string cardAccount = !string.IsNullOrWhiteSpace(dto.AccountFullName) ? dto.AccountFullName : null;

            // lee la cuenta seleccionada (esta cookie debe contener la cuenta de gasto
            // previamente seleccionada vía /api/quickbooks/select-account)
            string selectedExpenseAccount = null;
            if (Request.Cookies.TryGetValue(SelectedAccountCookieName, out var cookieValue) && !string.IsNullOrWhiteSpace(cookieValue))
            {
                selectedExpenseAccount = cookieValue;
            }

            // Determinar ExpenseAccount:
            // 1) usar dto.ExpenseAccountFullName si viene del cliente (la cuenta desde la tabla)
            // 2) si no, usar la cuenta guardada en cookie (selección del usuario)
            // 3) fallback razonable
            var expenseAccount = !string.IsNullOrWhiteSpace(dto.ExpenseAccountFullName)
                ? dto.ExpenseAccountFullName
                : (selectedExpenseAccount ?? "Uncategorized Expense");

            // Mapeo final:
            // - Ahora AccountFullName usará primero la cuenta leída desde la tabla (dto.ExpenseAccountFullName)
            //   si está presente; si no, caerá a la cuenta de tarjeta enviada (cardAccount) y luego a la cookie.
            // - ExpenseAccountFullName mantiene el valor resuelto (viene de la tabla o de la cookie).
            var mappedDto = new CreditCardChargeDto
            {
                TxnDate = dto.TxnDate == default ? DateTime.UtcNow : dto.TxnDate,
                PayeeFullName = dto.PayeeFullName,
                AccountFullName = !string.IsNullOrWhiteSpace(dto.ExpenseAccountFullName)
                    ? dto.ExpenseAccountFullName
                    : (cardAccount ?? (selectedExpenseAccount ?? "Uncategorized Expense")),
                Memo = dto.Memo,
                ExpenseAccountFullName = expenseAccount,
                Amount = dto.Amount
            };

            // Opcional: si quieres persistir la cuenta de gasto que vino en el payload,
            // puedes actualizar la cookie para próximas peticiones.
            if (!string.IsNullOrWhiteSpace(dto.ExpenseAccountFullName))
            {
                var opts = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    HttpOnly = false,
                    Path = "/"
                };
                Response.Cookies.Append(SelectedAccountCookieName, dto.ExpenseAccountFullName, opts);
            }

            var result = await _qb.CreateCreditCardChargeAsync(mappedDto);
            if (result.IsSuccess) return Ok(new { result.TxnId, message = "Charge recorded" });
            return BadRequest(new { error = result.Message });
        }

        public class RegistrarCargoDto
        {
            public string fecha { get; set; } = "";
            public string proveedor { get; set; } = "";
            public string cuenta { get; set; } = "";
            public string memo { get; set; } = "";
            public string cuentaGasto { get; set; } = "";
            public decimal monto { get; set; }
        }

        [HttpPost("registrar-cargo")]
        public IActionResult RegistrarCargo([FromBody] RegistrarCargoDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, message = "Payload vacío" });

            try
            {
                // Parse fecha seguro (fallback a ahora)
                DateTime fecha = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(dto.fecha))
                {
                    if (!DateTime.TryParse(dto.fecha, out fecha))
                        fecha = DateTime.UtcNow;
                }

                // Map to Movimiento (solo campos necesarios para IIF)
                var mov = new Movimiento
                {
                    Fecha = fecha,
                    Descripcion = dto.memo ?? "",
                    Monto = dto.monto,
                    Empresa = dto.proveedor ?? ""
                };

                // Use cuentaGasto / cuenta as CuentaContable (IIFBuilder uses CuentaContable)
                mov.CuentaContable = string.IsNullOrWhiteSpace(dto.cuentaGasto) ? (dto.cuenta ?? "Uncategorized Expense") : dto.cuentaGasto;

                // Build IIF content for this single movimiento
                var iif = _iifBuilder.BuildIIF(new List<Movimiento> { mov });

                // Ensure folder exists and persist file
                var exportDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "quickbooks");
                Directory.CreateDirectory(exportDir);

                var fileName = $"QB_REG_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.iif";
                var filePath = Path.Combine(exportDir, fileName);

                System.IO.File.WriteAllText(filePath, iif, Encoding.UTF8);

                var publicUrl = $"/quickbooks/{fileName}";

                _logger?.LogInformation("QuickBooks: wrote registration file {File}", filePath);

                return Ok(new { success = true, file = publicUrl });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error registering QuickBooks charge");
                return StatusCode(500, new { success = false, message = "Server error" });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] CreditCardChargeDto dto)
        {
            if (dto == null) return BadRequest(new { isSuccess = false, message = "Payload inválido" });

            var result = await _qb.CreateCreditCardChargeAsync(dto);

            if (result.IsSuccess)
                return Ok(new { isSuccess = true, txnId = result.TxnId });
            else
                return BadRequest(new { isSuccess = false, message = result.Message });
        }


    }
}