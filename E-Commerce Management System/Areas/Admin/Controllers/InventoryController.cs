using ECommerceManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")] // staff can also update stock
    public class InventoryController : Controller
    {
        public const int LowStockThreshold = 5;

        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/Inventory?search=shirt&lowOnly=true
        public async Task<IActionResult> Index(string? search, bool lowOnly = false)
        {
            var query = _context.ProductVariants.Include(v => v.Product).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(v => v.Product.Name.Contains(search)
                                      || v.Size.Contains(search)
                                      || v.Color.Contains(search));

            if (lowOnly)
                query = query.Where(v => v.StockQuantity <= LowStockThreshold);

            var variants = await query
                .OrderBy(v => v.StockQuantity)
                .ThenBy(v => v.Product.Name)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.LowOnly = lowOnly;
            ViewBag.Threshold = LowStockThreshold;
            return View(variants);
        }

        // POST: /Admin/Inventory/Update
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int variantId, int quantity, string? search, bool lowOnly = false)
        {
            if (quantity < 0)
            {
                TempData["Error"] = "Stock cannot be negative.";
                return RedirectToAction(nameof(Index), new { search, lowOnly });
            }

            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId);
            if (variant == null) return NotFound();

            variant.StockQuantity = quantity;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Stock for {variant.Product.Name} ({variant.Size} / {variant.Color}) set to {quantity}.";
            return RedirectToAction(nameof(Index), new { search, lowOnly });
        }
    }
}
