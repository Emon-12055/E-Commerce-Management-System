using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // "Today" and "this month" in Bangladesh time (UTC+6)
            var nowBd = DateTime.UtcNow.AddHours(6);
            var todayStartUtc = nowBd.Date.AddHours(-6);
            var monthStartUtc = new DateTime(nowBd.Year, nowBd.Month, 1).AddHours(-6);

            var salesOrders = _context.Orders.Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Returned);

            var vm = new DashboardViewModel
            {
                // Orders
                PendingOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending),
                ToPackOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Processing),
                ShippedOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Shipped),
                TodayOrders = await _context.Orders.CountAsync(o => o.OrderDate >= todayStartUtc),
                TodaySales = await salesOrders.Where(o => o.OrderDate >= todayStartUtc).SumAsync(o => (decimal?)o.Total) ?? 0,
                MonthSales = await salesOrders.Where(o => o.OrderDate >= monthStartUtc).SumAsync(o => (decimal?)o.Total) ?? 0,
                TotalOrders = await _context.Orders.CountAsync(),
                RecentOrders = await _context.Orders
                    .OrderByDescending(o => o.OrderDate)
                    .Take(8)
                    .ToListAsync(),

                // Catalog
                TotalProducts = await _context.Products.CountAsync(),
                ActiveProducts = await _context.Products.CountAsync(p => p.IsActive),
                TotalCategories = await _context.Categories.CountAsync(),

                // Stock: each variant has its own LowStockThreshold (default 5)
                LowStockThreshold = InventoryController.DefaultLowStockThreshold,
                LowStockItems = await _context.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => v.IsActive && v.StockQuantity <= v.LowStockThreshold)
                    .OrderBy(v => v.StockQuantity)
                    .Take(10)
                    .ToListAsync()
            };

            // People (Admin only)
            if (User.IsInRole("Admin"))
            {
                vm.TotalStaff = (await _userManager.GetUsersInRoleAsync("Staff")).Count;
                vm.TotalCustomers = (await _userManager.GetUsersInRoleAsync("Customer")).Count;
            }

            return View(vm);
        }
    }
}
