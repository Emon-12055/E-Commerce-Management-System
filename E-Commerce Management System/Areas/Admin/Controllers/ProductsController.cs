using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.Services;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IImageStorageService _images;

        public ProductsController(ApplicationDbContext context, IImageStorageService images)
        {
            _context = context;
            _images = images;
        }

        // GET: /Admin/Products?search=shirt&categoryId=2
        public async Task<IActionResult> Index(string? search, int? categoryId)
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.Contains(search));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId);

            var products = await query
                .OrderByDescending(p => p.Id)
                .Select(p => new ProductListItemViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    CategoryName = p.Category.Name,
                    Price = p.BasePrice,
                    IsActive = p.IsActive,
                    MainImageUrl = p.Images
                        .OrderByDescending(i => i.IsMain)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault(),
                    TotalStock = p.Variants.Sum(v => (int?)v.StockQuantity) ?? 0,
                    VariantCount = p.Variants.Count
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.Categories = await GetCategoryList(categoryId);
            return View(products);
        }

        // GET: /Admin/Products/Create
        public async Task<IActionResult> Create()
        {
            var vm = new ProductFormViewModel { Categories = await GetCategoryList() };
            return View("Form", vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductFormViewModel vm)
        {
            ValidateUploadedImages(vm.NewImages);

            if (!ModelState.IsValid)
            {
                vm.Categories = await GetCategoryList(vm.CategoryId);
                return View("Form", vm);
            }

            var product = new Product
            {
                Name = vm.Name.Trim(),
                Description = vm.Description?.Trim(),
                Slug = await MakeUniqueSlugAsync(vm.Name),
                BasePrice = vm.Price,
                CategoryId = vm.CategoryId!.Value,
                IsActive = vm.IsActive
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            await SaveImagesAsync(product.Id, vm.NewImages);

            TempData["Success"] = $"Product \"{product.Name}\" created. Now add its sizes, colors and stock below.";
            return RedirectToAction(nameof(Edit), new { id = product.Id });
        }

        // GET: /Admin/Products/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            var vm = new ProductFormViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.BasePrice,
                CategoryId = product.CategoryId,
                IsActive = product.IsActive
            };

            await LoadEditExtras(vm);
            return View("Form", vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductFormViewModel vm)
        {
            if (id != vm.Id) return BadRequest();

            ValidateUploadedImages(vm.NewImages);

            if (!ModelState.IsValid)
            {
                await LoadEditExtras(vm);
                return View("Form", vm);
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.Name = vm.Name.Trim();
            product.Description = vm.Description?.Trim();
            product.BasePrice = vm.Price;
            product.UpdatedAt = DateTime.UtcNow;
            product.CategoryId = vm.CategoryId!.Value;
            product.IsActive = vm.IsActive;
            await _context.SaveChangesAsync();

            await SaveImagesAsync(product.Id, vm.NewImages);

            TempData["Success"] = $"Product \"{product.Name}\" saved.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        // POST: /Admin/Products/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();

            var imageUrls = product.Images.Select(i => i.ImageUrl).ToList();

            try
            {
                _context.ProductImages.RemoveRange(product.Images);
                _context.ProductVariants.RemoveRange(product.Variants);
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                foreach (var url in imageUrls) _images.DeleteImage(url);
                TempData["Success"] = $"Product \"{product.Name}\" deleted.";
            }
            catch (DbUpdateException)
            {
                // The product is used in orders/carts, so we hide it instead of deleting it.
                _context.ChangeTracker.Clear();
                var p = await _context.Products.FindAsync(id);
                if (p != null)
                {
                    p.IsActive = false;
                    await _context.SaveChangesAsync();
                }
                TempData["Warning"] = "This product is used in existing orders, so it was hidden (set inactive) instead of deleted.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ---------- Images ----------

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMainImage(int imageId)
        {
            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null) return NotFound();

            var all = await _context.ProductImages.Where(i => i.ProductId == image.ProductId).ToListAsync();
            foreach (var img in all) img.IsMain = img.Id == imageId;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Main image updated.";
            return RedirectToAction(nameof(Edit), new { id = image.ProductId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null) return NotFound();

            var productId = image.ProductId;
            bool wasMain = image.IsMain;

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();
            _images.DeleteImage(image.ImageUrl);

            // If the main image was deleted, make the next one main
            if (wasMain)
            {
                var next = await _context.ProductImages.FirstOrDefaultAsync(i => i.ProductId == productId);
                if (next != null)
                {
                    next.IsMain = true;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Success"] = "Image deleted.";
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        // ---------- Variants (size / color / stock) ----------

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVariant(VariantFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please enter a size, a color and a stock of 0 or more.";
                return RedirectToAction(nameof(Edit), new { id = vm.ProductId });
            }

            var size = vm.Size.Trim();
            var color = vm.Color.Trim();

            bool exists = await _context.ProductVariants.AnyAsync(v =>
                v.ProductId == vm.ProductId && v.Size == size && v.Color == color);
            if (exists)
            {
                TempData["Error"] = $"Variant {size} / {color} already exists. Change its stock in Inventory.";
                return RedirectToAction(nameof(Edit), new { id = vm.ProductId });
            }

            _context.ProductVariants.Add(new ProductVariant
            {
                ProductId = vm.ProductId,
                SKU = await MakeUniqueSkuAsync(vm.ProductId, size, color),
                Size = size,
                Color = color,
                StockQuantity = vm.StockQuantity
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Variant {size} / {color} added.";
            return RedirectToAction(nameof(Edit), new { id = vm.ProductId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVariant(int variantId)
        {
            var variant = await _context.ProductVariants.FindAsync(variantId);
            if (variant == null) return NotFound();

            var productId = variant.ProductId;
            try
            {
                _context.ProductVariants.Remove(variant);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Variant deleted.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "This variant is used in carts or orders and cannot be deleted. Set its stock to 0 instead.";
            }

            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        // ---------- Helpers ----------

        // "Men's Black T-Shirt" -> "mens-black-t-shirt" (adds -2, -3... if it already exists)
        private async Task<string> MakeUniqueSlugAsync(string name)
        {
            var chars = name.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();
            var baseSlug = string.Join("-", new string(chars)
                .Split('-', StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrEmpty(baseSlug)) baseSlug = "product";
            if (baseSlug.Length > 200) baseSlug = baseSlug[..200];

            var slug = baseSlug;
            int i = 2;
            while (await _context.Products.AnyAsync(p => p.Slug == slug))
                slug = $"{baseSlug}-{i++}";
            return slug;
        }

        // Product 12, size M, color Black -> "P12-M-BLACK"
        private async Task<string> MakeUniqueSkuAsync(int productId, string size, string color)
        {
            static string Clean(string x) =>
                new string(x.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

            var baseSku = $"P{productId}-{Clean(size)}-{Clean(color)}";
            if (baseSku.Length > 45) baseSku = baseSku[..45];

            var sku = baseSku;
            int i = 2;
            while (await _context.ProductVariants.AnyAsync(v => v.SKU == sku))
                sku = $"{baseSku}-{i++}";
            return sku;
        }

        private async Task<List<SelectListItem>> GetCategoryList(int? selectedId = null)
        {
            return await _context.Categories
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name,
                    Selected = c.Id == selectedId
                })
                .ToListAsync();
        }

        private async Task LoadEditExtras(ProductFormViewModel vm)
        {
            vm.Categories = await GetCategoryList(vm.CategoryId);
            vm.ExistingImages = await _context.ProductImages
                .Where(i => i.ProductId == vm.Id)
                .OrderByDescending(i => i.IsMain).ThenBy(i => i.Id)
                .ToListAsync();
            vm.Variants = await _context.ProductVariants
                .Where(v => v.ProductId == vm.Id)
                .OrderBy(v => v.Size).ThenBy(v => v.Color)
                .ToListAsync();
        }

        private void ValidateUploadedImages(IEnumerable<IFormFile>? files)
        {
            if (files == null) return;
            foreach (var file in files.Where(f => f != null && f.Length > 0))
            {
                var error = _images.Validate(file);
                if (error != null) ModelState.AddModelError(nameof(ProductFormViewModel.NewImages), error);
            }
        }

        private async Task SaveImagesAsync(int productId, IEnumerable<IFormFile>? files)
        {
            if (files == null) return;

            bool hasMain = await _context.ProductImages.AnyAsync(i => i.ProductId == productId && i.IsMain);

            foreach (var file in files.Where(f => f != null && f.Length > 0))
            {
                var url = await _images.SaveProductImageAsync(file);
                _context.ProductImages.Add(new ProductImage
                {
                    ProductId = productId,
                    ImageUrl = url,
                    IsMain = !hasMain // first image becomes the main image
                });
                hasMain = true;
            }

            await _context.SaveChangesAsync();
        }
    }
}
