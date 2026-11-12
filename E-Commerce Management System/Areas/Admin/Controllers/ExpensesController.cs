using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Areas.Admin.Controllers
{
    // Phase 8: record business expenses (Admin only)
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExpensesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private DbSet<Expense> Expenses => _context.Set<Expense>();

        // GET: /Admin/Expenses?from=2026-11-01&to=2026-11-30&category=Rent
        public async Task<IActionResult> Index(DateTime? from, DateTime? to, ExpenseCategory? category)
        {
            var todayBd = DateTime.UtcNow.AddHours(6).Date;
            var f = (from ?? new DateTime(todayBd.Year, todayBd.Month, 1)).Date;
            var t = (to ?? todayBd).Date;
            if (t < f) (f, t) = (t, f);

            var query = Expenses.Where(e => e.Date >= f && e.Date <= t);
            if (category.HasValue) query = query.Where(e => e.Category == category);

            var list = await query.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).ToListAsync();

            var vm = new ExpenseListViewModel
            {
                Range = new DateRangeViewModel { From = f, To = t, Action = "Index" },
                Category = category,
                Expenses = list,
                ByCategory = list
                    .GroupBy(e => e.Category)
                    .Select(g => new NameAmountRow { Name = ReportDisplay.Label(g.Key), Count = g.Count(), Amount = g.Sum(e => e.Amount) })
                    .OrderByDescending(r => r.Amount).ToList()
            };
            return View(vm);
        }

        public IActionResult Create() => View("Form", new Expense());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Date,Category,Description,Amount")] Expense expense)
        {
            if (!ModelState.IsValid) return View("Form", expense);

            expense.Date = expense.Date.Date;
            expense.Description = expense.Description.Trim();
            expense.CreatedByUserId = _userManager.GetUserId(User);
            expense.CreatedAt = DateTime.UtcNow;

            Expenses.Add(expense);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Expense of ৳ {expense.Amount:N0} saved.";
            return RedirectToAction(nameof(Index), new { from = expense.Date.ToString("yyyy-MM-01") });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var expense = await Expenses.FindAsync(id);
            if (expense == null) return NotFound();
            return View("Form", expense);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Date,Category,Description,Amount")] Expense form)
        {
            if (id != form.Id) return BadRequest();
            if (!ModelState.IsValid) return View("Form", form);

            var expense = await Expenses.FindAsync(id);
            if (expense == null) return NotFound();

            expense.Date = form.Date.Date;
            expense.Category = form.Category;
            expense.Description = form.Description.Trim();
            expense.Amount = form.Amount;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Expense updated.";
            return RedirectToAction(nameof(Index), new { from = expense.Date.ToString("yyyy-MM-01") });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var expense = await Expenses.FindAsync(id);
            if (expense != null)
            {
                Expenses.Remove(expense);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Expense deleted.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
