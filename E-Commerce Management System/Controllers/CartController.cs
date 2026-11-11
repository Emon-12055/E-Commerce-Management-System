using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartService _cart;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public CartController(ICartService cart, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _cart = cart;
            _userManager = userManager;
            _context = context;
        }

        private string UserId => _userManager.GetUserId(User)!;

        // GET: /Cart
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var cart = await _cart.GetCartAsync(UserId) ?? new Cart();
            return View(cart);
        }

        // POST: /Cart/Add   (from the product details page)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int variantId, int quantity = 1)
        {
            var productId = await _context.ProductVariants
                .Where(v => v.Id == variantId)
                .Select(v => (int?)v.ProductId)
                .FirstOrDefaultAsync();
            if (productId == null) return NotFound();

            // Not logged in: go to login, then come back to the same product
            if (User.Identity?.IsAuthenticated != true)
            {
                var returnUrl = Url.Action("Details", "Shop", new { id = productId });
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            var result = await _cart.AddAsync(UserId, variantId, quantity);

            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Details", "Shop", new { id = productId });
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Update
        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int cartItemId, int quantity)
        {
            var result = await _cart.UpdateQuantityAsync(UserId, cartItemId, quantity);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Remove
        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int cartItemId)
        {
            await _cart.RemoveAsync(UserId, cartItemId);
            TempData["Success"] = "Item removed from your cart.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Clear
        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            await _cart.ClearAsync(UserId);
            TempData["Success"] = "Your cart is now empty.";
            return RedirectToAction(nameof(Index));
        }
    }
}
