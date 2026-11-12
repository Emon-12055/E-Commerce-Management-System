using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.Services;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Areas.Admin.Controllers
{
    // Phase 7: order processing for Admin and Staff
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class OrdersController : Controller
    {
        private const int PageSize = 20;

        private readonly ApplicationDbContext _context;
        private readonly IOrderService _orders;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrdersController(ApplicationDbContext context, IOrderService orders, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _orders = orders;
            _userManager = userManager;
        }

        // GET: /Admin/Orders?status=Pending&search=01711&from=2026-11-01&to=2026-11-30&page=1
        public async Task<IActionResult> Index(OrderStatus? status, string? search, DateTime? from, DateTime? to, int page = 1)
        {
            var query = _context.Orders.AsQueryable();

            if (status.HasValue)
                query = query.Where(o => o.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(o => o.OrderNumber.Contains(s)
                                      || o.ShippingFullName.Contains(s)
                                      || o.ShippingPhone.Contains(s));
            }

            // Dates are chosen in Bangladesh time (UTC+6); the database stores UTC
            if (from.HasValue)
            {
                var fromUtc = from.Value.Date.AddHours(-6);
                query = query.Where(o => o.OrderDate >= fromUtc);
            }
            if (to.HasValue)
            {
                var toUtc = to.Value.Date.AddDays(1).AddHours(-6);
                query = query.Where(o => o.OrderDate < toUtc);
            }

            int total = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(o => new AdminOrderRowViewModel
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    CustomerName = o.ShippingFullName,
                    Phone = o.ShippingPhone,
                    City = o.ShippingCity,
                    ItemCount = o.Items.Sum(i => i.Quantity),
                    Total = o.Total,
                    Status = o.Status,
                    PaymentMethod = o.Payments.OrderByDescending(p => p.Id).Select(p => (PaymentMethod?)p.Method).FirstOrDefault(),
                    PaymentStatus = o.Payments.OrderByDescending(p => p.Id).Select(p => (PaymentStatus?)p.Status).FirstOrDefault()
                })
                .ToListAsync();

            // Count per status for the tabs
            ViewBag.Counts = await _context.Orders
                .GroupBy(o => o.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            ViewBag.Status = status;
            ViewBag.Search = search;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Total = total;

            return View(orders);
        }

        // GET: /Admin/Orders/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            ViewBag.NextStatuses = OrderService.NextStatuses(order.Status);
            return View(order);
        }

        // POST: /Admin/Orders/UpdateStatus
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus newStatus, string? note)
        {
            var userId = _userManager.GetUserId(User)!;
            var (success, message) = await _orders.UpdateStatusAsync(id, newStatus, userId, note);

            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Admin/Orders/Invoice/5   (printable invoice / packing slip)
        public async Task<IActionResult> Invoice(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            return View(order);
        }
    }
}
