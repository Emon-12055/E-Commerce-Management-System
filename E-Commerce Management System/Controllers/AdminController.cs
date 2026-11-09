using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]   // ONLY Admin can open anything here
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ---------- DASHBOARD ----------
        public async Task<IActionResult> Index()
        {
            ViewBag.ProductCount = await _db.Products.CountAsync();
            ViewBag.CategoryCount = await _db.Categories.CountAsync();
            ViewBag.OrderCount = await _db.Orders.CountAsync();
            ViewBag.CustomerCount = (await _userManager.GetUsersInRoleAsync(AppRoles.Customer)).Count;
            ViewBag.StaffCount = (await _userManager.GetUsersInRoleAsync(AppRoles.Staff)).Count;
            return View();
        }

        // ---------- ALL USERS ----------
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
            var list = new List<UserListItemViewModel>();

            foreach (var u in users)
            {
                list.Add(new UserListItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? "",
                    PhoneNumber = u.PhoneNumber,
                    Roles = string.Join(", ", await _userManager.GetRolesAsync(u)),
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                });
            }

            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            return View(list);
        }

        // ---------- CREATE STAFF / ADMIN ----------
        [HttpGet]
        public IActionResult CreateStaff()
        {
            return View(new CreateStaffViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(CreateStaffViewModel model)
        {
            if (model.Role != AppRoles.Staff && model.Role != AppRoles.Admin)
                ModelState.AddModelError(nameof(model.Role), "Invalid role.");

            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                TempData["Success"] = model.Role + " account created for " + model.Email;
                return RedirectToAction(nameof(Users));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // ---------- ENABLE / DISABLE USER ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["Error"] = "You cannot disable your own account.";
                return RedirectToAction(nameof(Users));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            await _userManager.UpdateSecurityStampAsync(user);   // logs the user out

            TempData["Success"] = user.Email + (user.IsActive ? " enabled." : " disabled.");
            return RedirectToAction(nameof(Users));
        }
    }
}