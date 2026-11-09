using System.Threading.Tasks;

using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Controllers
{
    [Authorize(Roles = AppRoles.AdminOrStaff)]   // Admin and Staff can open this
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _db;

        public StaffController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
            ViewBag.ProcessingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Processing);
            ViewBag.LowStock = await _db.ProductVariants.CountAsync(v => v.StockQuantity <= v.LowStockThreshold);
            ViewBag.ProductCount = await _db.Products.CountAsync();
            return View();
        }
    }
}