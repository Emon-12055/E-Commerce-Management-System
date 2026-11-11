using ECommerceManagementSystem.Controllers;
using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.ViewComponents
{
    // Loads categories, featured products and new arrivals for the Home page.
    // Used in Views/Home/Index.cshtml, so HomeController does not need to change.
    public class HomeProductsViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public HomeProductsViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var vm = new HomeProductsViewModel
            {
                Categories = await _context.Categories.OrderBy(c => c.Name).Take(8).ToListAsync(),

                Featured = await _context.Products
                    .Where(p => p.IsActive && p.IsFeatured)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(8)
                    .Select(ShopController.ToCard)
                    .ToListAsync(),

                NewArrivals = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                    .Take(8)
                    .Select(ShopController.ToCard)
                    .ToListAsync()
            };

            return View(vm);
        }
    }
}
