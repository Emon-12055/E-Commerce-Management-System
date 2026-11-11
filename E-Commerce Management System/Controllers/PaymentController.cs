using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Controllers
{
    // SSLCommerz sends the customer back here after payment.
    // These requests come from the gateway, so no login or anti-forgery token is required.
    // The payment is always double-checked with SSLCommerz before marking it as paid.
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISslCommerzService _ssl;

        public PaymentController(ApplicationDbContext context, ISslCommerzService ssl)
        {
            _context = context;
            _ssl = ssl;
        }

        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> Success([FromForm(Name = "tran_id")] string? tranId, [FromForm(Name = "val_id")] string? valId)
        {
            tranId ??= Request.Query["tran_id"];
            valId ??= Request.Query["val_id"];

            var payment = await FindPaymentAsync(tranId);
            if (payment == null) return NotFound();

            bool paid = await ConfirmPaymentAsync(payment, valId);

            TempData[paid ? "Success" : "Error"] = paid
                ? $"Payment received. Thank you! Your order {payment.Order!.OrderNumber} is confirmed."
                : "We could not verify your payment. If money was taken, please contact us with your order number.";

            return RedirectToAction("Details", "Orders", new { id = payment.OrderId });
        }

        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> Fail([FromForm(Name = "tran_id")] string? tranId)
        {
            return await MarkNotPaidAsync(tranId ?? Request.Query["tran_id"],
                "Payment failed. Your order is saved, so you can try again with \"Pay now\".");
        }

        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> Cancel([FromForm(Name = "tran_id")] string? tranId)
        {
            return await MarkNotPaidAsync(tranId ?? Request.Query["tran_id"],
                "Payment was cancelled. Your order is saved, so you can pay later with \"Pay now\" or cancel the order.");
        }

        // Server-to-server message from SSLCommerz (works only when the site is online, not on localhost)
        [HttpPost]
        public async Task<IActionResult> Ipn([FromForm(Name = "tran_id")] string? tranId, [FromForm(Name = "val_id")] string? valId)
        {
            var payment = await FindPaymentAsync(tranId);
            if (payment != null) await ConfirmPaymentAsync(payment, valId);
            return Ok();
        }

        // ---------- Helpers ----------

        private async Task<Payment?> FindPaymentAsync(string? tranId)
        {
            if (string.IsNullOrWhiteSpace(tranId)) return null;
            return await _context.Set<Payment>()
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.TransactionId == tranId);
        }

        private async Task<bool> ConfirmPaymentAsync(Payment payment, string? valId)
        {
            if (payment.Status == PaymentStatus.Paid) return true;

            var check = await _ssl.ValidateAsync(valId ?? "");
            bool ok = check.IsValid
                      && check.TransactionId == payment.TransactionId
                      && Math.Abs(check.Amount - payment.Amount) < 1m;

            payment.GatewayResponse = check.RawResponse;
            if (ok)
            {
                payment.Status = PaymentStatus.Paid;
                payment.PaidAt = DateTime.UtcNow;
                if (payment.Order != null && payment.Order.Status == OrderStatus.Pending)
                    payment.Order.Status = OrderStatus.Confirmed;
            }
            else
            {
                payment.Status = PaymentStatus.Failed;
            }

            await _context.SaveChangesAsync();
            return ok;
        }

        private async Task<IActionResult> MarkNotPaidAsync(string? tranId, string message)
        {
            var payment = await FindPaymentAsync(tranId);
            if (payment == null) return RedirectToAction("Index", "Orders");

            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
            }

            TempData["Error"] = message;
            return RedirectToAction("Details", "Orders", new { id = payment.OrderId });
        }
    }
}
