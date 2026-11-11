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
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly ICartService _cart;
        private readonly IOrderService _orders;
        private readonly ISslCommerzService _ssl;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public CheckoutController(ICartService cart, IOrderService orders, ISslCommerzService ssl,
                                  UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _cart = cart;
            _orders = orders;
            _ssl = ssl;
            _userManager = userManager;
            _context = context;
        }

        // GET: /Checkout
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var cart = await _cart.GetCartAsync(user.Id);
            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            // Fill the form with the customer's saved details
            var vm = new CheckoutViewModel
            {
                FullName = user.FullName,
                Phone = user.PhoneNumber ?? string.Empty,
                Address = user.Address ?? string.Empty,
                City = user.City ?? string.Empty,
                Cart = cart,
                OnlinePaymentEnabled = _ssl.IsConfigured
            };
            return View(vm);
        }

        // POST: /Checkout
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CheckoutViewModel form)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            bool online = form.PaymentMethod != PaymentMethod.CashOnDelivery;
            if (online && (form.PaymentMethod != PaymentMethod.SSLCommerz || !_ssl.IsConfigured))
                ModelState.AddModelError(nameof(form.PaymentMethod), "This payment method is not available.");

            if (!ModelState.IsValid)
            {
                form.Cart = await _cart.GetCartAsync(user.Id);
                form.OnlinePaymentEnabled = _ssl.IsConfigured;
                return View(form);
            }

            var result = await _orders.PlaceOrderAsync(user.Id, form);
            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction("Index", "Cart");
            }

            var order = result.Order!;

            // Save phone/address/city on the profile so next checkout is faster
            user.PhoneNumber ??= form.Phone;
            user.Address ??= form.Address;
            user.City ??= form.City;
            await _userManager.UpdateAsync(user);

            // ----- Cash on Delivery: done
            if (!online)
            {
                TempData["Success"] = $"Thank you! Your order {order.OrderNumber} has been placed. Pay ৳ {order.Total:N0} when it arrives.";
                return RedirectToAction("Details", "Orders", new { id = order.Id });
            }

            // ----- Online payment: send the customer to SSLCommerz
            var payment = order.Payments.First();
            var init = await _ssl.InitiateAsync(order, payment, user, $"{Request.Scheme}://{Request.Host}");
            if (!init.Success)
            {
                payment.Status = PaymentStatus.Failed;
                payment.GatewayResponse = init.Error;
                await _context.SaveChangesAsync();

                TempData["Error"] = $"Your order {order.OrderNumber} was saved, but the payment could not start: {init.Error} You can try again with \"Pay now\".";
                return RedirectToAction("Details", "Orders", new { id = order.Id });
            }

            return Redirect(init.GatewayUrl!);
        }
    }
}
