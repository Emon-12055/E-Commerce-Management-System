using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.Services;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Controllers
{
    // Customer: order history, order details/tracking, cancel, pay again
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderService _orders;
        private readonly ISslCommerzService _ssl;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrdersController(ApplicationDbContext context, IOrderService orders, ISslCommerzService ssl,
                                UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _orders = orders;
            _ssl = ssl;
            _userManager = userManager;
        }

        private string UserId => _userManager.GetUserId(User)!;

        // GET: /Orders   (order history)
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .Where(o => o.UserId == UserId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderListItemViewModel
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    Total = o.Total,
                    ItemCount = o.Items.Sum(i => i.Quantity),
                    PaymentMethod = o.Payments.OrderByDescending(p => p.Id).Select(p => (PaymentMethod?)p.Method).FirstOrDefault(),
                    PaymentStatus = o.Payments.OrderByDescending(p => p.Id).Select(p => (PaymentStatus?)p.Status).FirstOrDefault()
                })
                .ToListAsync();

            return View(orders);
        }

        // GET: /Orders/Details/5   (details + tracking)
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);
            if (order == null) return NotFound();

            return View(order);
        }

        // GET: /Orders/Track?orderNumber=ORD-20261112-12345
        public async Task<IActionResult> Track(string? orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber)) return RedirectToAction(nameof(Index));

            var id = await _context.Orders
                .Where(o => o.UserId == UserId && o.OrderNumber == orderNumber.Trim())
                .Select(o => (int?)o.Id)
                .FirstOrDefaultAsync();

            if (id == null)
            {
                TempData["Error"] = $"No order found with number \"{orderNumber}\".";
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Orders/Cancel/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var (success, message) = await _orders.CancelOrderAsync(id, UserId, UserId, "Cancelled by customer");
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Orders/Pay/5   (try online payment again)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);
            if (order == null) return NotFound();

            var payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
            if (order.Status != OrderStatus.Pending || payment == null
                || payment.Method == PaymentMethod.CashOnDelivery || payment.Status == PaymentStatus.Paid)
            {
                TempData["Error"] = "This order does not need an online payment.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // New transaction id for every attempt
            payment.TransactionId = _orders.NewTransactionId();
            payment.Status = PaymentStatus.Pending;
            await _context.SaveChangesAsync();

            var init = await _ssl.InitiateAsync(order, payment, user, $"{Request.Scheme}://{Request.Host}");
            if (!init.Success)
            {
                payment.Status = PaymentStatus.Failed;
                await _context.SaveChangesAsync();
                TempData["Error"] = init.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            return Redirect(init.GatewayUrl!);
        }
    }
}
