using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/Categories
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .Select(c => new CategoryFormViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ProductCount = c.Products.Count
                })
                .ToListAsync();

            return View(categories);
        }

        // GET: /Admin/Categories/Create
        public IActionResult Create()
        {
            return View("Form", new CategoryFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryFormViewModel vm)
        {
            await CheckDuplicateName(vm);
            if (!ModelState.IsValid) return View("Form", vm);

            _context.Categories.Add(new Category
            {
                Name = vm.Name.Trim(),
                Slug = await MakeUniqueSlugAsync(vm.Name, 0),
                Description = vm.Description?.Trim()
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Category \"{vm.Name}\" created.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Categories/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return View("Form", new CategoryFormViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryFormViewModel vm)
        {
            if (id != vm.Id) return BadRequest();

            await CheckDuplicateName(vm);
            if (!ModelState.IsValid) return View("Form", vm);

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            category.Name = vm.Name.Trim();
            category.Description = vm.Description?.Trim();
            if (string.IsNullOrEmpty(category.Slug))
                category.Slug = await MakeUniqueSlugAsync(category.Name, category.Id);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Category \"{category.Name}\" updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Categories/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
            {
                TempData["Error"] = $"\"{category.Name}\" still has products. Move or delete them first.";
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Category \"{category.Name}\" deleted.";
            return RedirectToAction(nameof(Index));
        }

        // "Men's Wear" -> "men-s-wear" (adds -2, -3... if it already exists)
        private async Task<string> MakeUniqueSlugAsync(string name, int currentId)
        {
            var chars = name.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();
            var baseSlug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrEmpty(baseSlug)) baseSlug = "category";
            if (baseSlug.Length > 100) baseSlug = baseSlug[..100];

            var slug = baseSlug;
            int i = 2;
            while (await _context.Categories.AnyAsync(c => c.Slug == slug && c.Id != currentId))
                slug = $"{baseSlug}-{i++}";
            return slug;
        }

        private async Task CheckDuplicateName(CategoryFormViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Name)) return;

            var name = vm.Name.Trim();
            bool exists = await _context.Categories.AnyAsync(c => c.Name == name && c.Id != vm.Id);
            if (exists) ModelState.AddModelError(nameof(vm.Name), "A category with this name already exists.");
        }
    }
}
