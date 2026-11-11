using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceManagementSystem.ViewComponents
{
    // Shows "Cart (3)" in the navbar. Used in Views/Shared/_Layout.cshtml
    public class CartSummaryViewComponent : ViewComponent
    {
        private readonly ICartService _cart;
        private readonly UserManager<ApplicationUser> _userManager;

        public CartSummaryViewComponent(ICartService cart, UserManager<ApplicationUser> userManager)
        {
            _cart = cart;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int count = 0;
            var userId = _userManager.GetUserId(HttpContext.User);
            if (userId != null) count = await _cart.GetItemCountAsync(userId);

            return View(count);
        }
    }
}
