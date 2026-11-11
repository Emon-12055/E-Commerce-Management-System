using System.Linq.Expressions;
using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Controllers
{
    // Customer website: product listing, search, filters and product details.
    // Open to everyone (no login needed).
    public class ShopController : Controller
    {
        private const int PageSize = 12;
        private readonly ApplicationDbContext _context;

        public ShopController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Converts a Product into a small card (used here and on the Home page)
        public static readonly Expression<Func<Product, ProductCardViewModel>> ToCard = p => new ProductCardViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Brand = p.Brand,
            CategoryName = p.Category != null ? p.Category.Name : null,
            Price = p.BasePrice,
            CompareAtPrice = p.CompareAtPrice,
            ImageUrl = p.Images
                .OrderByDescending(i => i.IsMain).ThenBy(i => i.Id)
                .Select(i => i.ImageUrl)
                .FirstOrDefault(),
            InStock = p.Variants.Any(v => v.IsActive && v.StockQuantity > 0)
        };

        // GET: /Shop?search=shirt&categoryId=1&minPrice=500&maxPrice=2000&size=M&color=Black&sort=price_asc&page=2
        public async Task<IActionResult> Index(ShopIndexViewModel filter)
        {
            var query = _context.Products.Where(p => p.IsActive);

            // ----- Search
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                query = query.Where(p => p.Name.Contains(s)
                                      || (p.Description != null && p.Description.Contains(s))
                                      || (p.Brand != null && p.Brand.Contains(s))
                                      || (p.Category != null && p.Category.Name.Contains(s)));
            }

            // ----- Filters
            if (filter.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == filter.CategoryId);

            if (filter.MinPrice.HasValue)
                query = query.Where(p => p.BasePrice >= filter.MinPrice);

            if (filter.MaxPrice.HasValue)
                query = query.Where(p => p.BasePrice <= filter.MaxPrice);

            if (!string.IsNullOrWhiteSpace(filter.Size))
                query = query.Where(p => p.Variants.Any(v => v.IsActive && v.Size == filter.Size && v.StockQuantity > 0));

            if (!string.IsNullOrWhiteSpace(filter.Color))
                query = query.Where(p => p.Variants.Any(v => v.IsActive && v.Color == filter.Color && v.StockQuantity > 0));

            if (filter.InStockOnly)
                query = query.Where(p => p.Variants.Any(v => v.IsActive && v.StockQuantity > 0));

            // ----- Sorting
            query = filter.Sort switch
            {
                "price_asc" => query.OrderBy(p => p.BasePrice),
                "price_desc" => query.OrderByDescending(p => p.BasePrice),
                "name" => query.OrderBy(p => p.Name),
                _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id) // newest
            };

            // ----- Paging
            filter.TotalCount = await query.CountAsync();
            filter.TotalPages = Math.Max(1, (int)Math.Ceiling(filter.TotalCount / (double)PageSize));
            filter.Page = Math.Clamp(filter.Page, 1, filter.TotalPages);

            filter.Products = await query
                .Skip((filter.Page - 1) * PageSize)
                .Take(PageSize)
                .Select(ToCard)
                .ToListAsync();

            // ----- Data for the filter sidebar
            filter.Categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync();

            var activeVariants = _context.ProductVariants.Where(v => v.IsActive && v.Product!.IsActive);
            filter.Sizes = await activeVariants.Select(v => v.Size).Distinct().OrderBy(x => x).ToListAsync();
            filter.Colors = await activeVariants.Select(v => v.Color).Distinct().OrderBy(x => x).ToListAsync();

            return View(filter);
        }

        // GET: /Shop/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (product == null) return NotFound();

            var vm = new ProductDetailsViewModel
            {
                Product = product,
                Images = await _context.ProductImages
                    .Where(i => i.ProductId == id)
                    .OrderByDescending(i => i.IsMain).ThenBy(i => i.Id)
                    .ToListAsync(),
                Variants = await _context.ProductVariants
                    .Where(v => v.ProductId == id && v.IsActive)
                    .OrderBy(v => v.Size).ThenBy(v => v.Color)
                    .Select(v => new VariantOption
                    {
                        Id = v.Id,
                        Size = v.Size,
                        Color = v.Color,
                        Stock = v.StockQuantity,
                        Price = v.Price ?? product.BasePrice
                    })
                    .ToListAsync(),
                Related = await _context.Products
                    .Where(p => p.IsActive && p.CategoryId == product.CategoryId && p.Id != id)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(4)
                    .Select(ToCard)
                    .ToListAsync()
            };

            vm.Sizes = vm.Variants.Select(v => v.Size).Distinct().ToList();
            vm.Colors = vm.Variants.Select(v => v.Color).Distinct().ToList();

            return View(vm);
        }
    }
}
