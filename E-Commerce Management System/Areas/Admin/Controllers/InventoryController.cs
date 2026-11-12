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
    [Authorize(Roles = "Admin,Staff")] // staff can also manage stock
    public class InventoryController : Controller
    {
        public const int DefaultLowStockThreshold = 5;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InventoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Admin/Inventory?search=shirt&lowOnly=true
        public async Task<IActionResult> Index(string? search, bool lowOnly = false)
        {
            var query = _context.ProductVariants.Include(v => v.Product).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(v => v.Product!.Name.Contains(search)
                                      || v.SKU.Contains(search)
                                      || v.Size.Contains(search)
                                      || v.Color.Contains(search));

            if (lowOnly)
                query = query.Where(v => v.StockQuantity <= v.LowStockThreshold);

            var variants = await query
                .OrderBy(v => v.StockQuantity)
                .ThenBy(v => v.Product!.Name)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.LowOnly = lowOnly;
            return View(variants);
        }

        // POST: /Admin/Inventory/Update
        // mode = "add" -> new stock arrived (Stock In), mode = "set" -> correct the count (Adjustment)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int variantId, int quantity, string mode, string? note, string? search, bool lowOnly = false)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId);
            if (variant == null) return NotFound();

            int oldStock = variant.StockQuantity;
            int newStock;
            InventoryChangeType type;

            if (mode == "add")
            {
                if (quantity <= 0)
                {
                    TempData["Error"] = "Enter how many pieces arrived (1 or more).";
                    return RedirectToAction(nameof(Index), new { search, lowOnly });
                }
                newStock = oldStock + quantity;
                type = InventoryChangeType.StockIn;
            }
            else
            {
                if (quantity < 0)
                {
                    TempData["Error"] = "Stock cannot be negative.";
                    return RedirectToAction(nameof(Index), new { search, lowOnly });
                }
                newStock = quantity;
                type = InventoryChangeType.Adjustment;
            }

            if (newStock == oldStock)
            {
                TempData["Warning"] = "Stock is unchanged.";
                return RedirectToAction(nameof(Index), new { search, lowOnly });
            }

            variant.StockQuantity = newStock;
            _context.Set<InventoryTransaction>().Add(new InventoryTransaction
            {
                ProductVariantId = variant.Id,
                ChangeType = type,
                QuantityChange = newStock - oldStock,
                StockAfter = newStock,
                Note = string.IsNullOrWhiteSpace(note)
                    ? (type == InventoryChangeType.StockIn ? "New stock received" : "Stock count corrected")
                    : note.Trim(),
                CreatedByUserId = _userManager.GetUserId(User),
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{variant.Product?.Name} ({variant.Size} / {variant.Color}): stock {oldStock} → {newStock}.";
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone placed an order for this item at the same moment
                TempData["Error"] = "This item's stock just changed (a customer may have ordered it). Please try again.";
            }

            return RedirectToAction(nameof(Index), new { search, lowOnly });
        }

        // GET: /Admin/Inventory/History?variantId=12
        public async Task<IActionResult> History(int? variantId)
        {
            var query = _context.Set<InventoryTransaction>().AsQueryable();
            if (variantId.HasValue) query = query.Where(t => t.ProductVariantId == variantId);

            var rows = await query
                .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                .Take(200)
                .Select(t => new InventoryHistoryRowViewModel
                {
                    CreatedAt = t.CreatedAt,
                    ProductName = t.ProductVariant!.Product!.Name,
                    Size = t.ProductVariant.Size,
                    Color = t.ProductVariant.Color,
                    SKU = t.ProductVariant.SKU,
                    ChangeType = t.ChangeType,
                    QuantityChange = t.QuantityChange,
                    StockAfter = t.StockAfter,
                    OrderId = t.OrderId,
                    OrderNumber = t.Order != null ? t.Order.OrderNumber : null,
                    Note = t.Note,
                    UserName = t.CreatedByUserId
                })
                .ToListAsync();

            // Show names instead of user ids
            var userIds = rows.Where(r => r.UserName != null).Select(r => r.UserName!).Distinct().ToList();
            var names = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => string.IsNullOrEmpty(u.FullName) ? (u.Email ?? u.Id) : u.FullName);
            foreach (var r in rows)
                if (r.UserName != null && names.TryGetValue(r.UserName, out var name)) r.UserName = name;

            if (variantId.HasValue)
            {
                var v = await _context.ProductVariants.Include(x => x.Product).FirstOrDefaultAsync(x => x.Id == variantId);
                ViewBag.Title = v == null ? "Stock history" : $"Stock history: {v.Product?.Name} ({v.Size} / {v.Color})";
            }

            return View(rows);
        }
    }
}
