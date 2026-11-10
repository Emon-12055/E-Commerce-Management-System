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
            var threshold = InventoryController.LowStockThreshold;

            var vm = new DashboardViewModel
            {
                TotalProducts = await _context.Products.CountAsync(),
                ActiveProducts = await _context.Products.CountAsync(p => p.IsActive),
                TotalCategories = await _context.Categories.CountAsync(),
                TotalOrders = await _context.Orders.CountAsync(),
                TotalStaff = (await _userManager.GetUsersInRoleAsync("Staff")).Count,
                TotalCustomers = (await _userManager.GetUsersInRoleAsync("Customer")).Count,
                LowStockThreshold = threshold,
                LowStockItems = await _context.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => v.StockQuantity <= threshold)
                    .OrderBy(v => v.StockQuantity)
                    .Take(10)
                    .ToListAsync()
            };

            return View(vm);
        }
    }
}
