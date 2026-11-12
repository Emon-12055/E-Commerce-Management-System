using System.Globalization;
using System.Text;
using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Areas.Admin.Controllers
{
    // Phase 8: sales and profit reports (Admin only)
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index() => RedirectToAction(nameof(Sales));

        // GET: /Admin/Reports/Sales?from=2026-11-01&to=2026-11-30
        public async Task<IActionResult> Sales(DateTime? from, DateTime? to)
        {
            var range = MakeRange(from, to, nameof(Sales));
            var orders = await LoadOrdersAsync(range);
            var sales = orders.Where(o => o.CountsAsSale).ToList();
            var items = sales.SelectMany(o => o.Items).ToList();

            var vm = new SalesReportViewModel
            {
                Range = range,
                OrderCount = sales.Count,
                ItemsSold = items.Sum(i => i.Quantity),
                ProductSales = sales.Sum(o => o.SubTotal),
                ShippingIncome = sales.Sum(o => o.ShippingCost),
                Discounts = sales.Sum(o => o.Discount),
                Revenue = sales.Sum(o => o.Total),
                CancelledCount = orders.Count(o => o.Status == OrderStatus.Cancelled),
                CancelledValue = orders.Where(o => o.Status == OrderStatus.Cancelled).Sum(o => o.Total),
                ReturnedCount = orders.Count(o => o.Status == OrderStatus.Returned),

                // One row per day (including days with no sales)
                Daily = Enumerable.Range(0, (range.To - range.From).Days + 1)
                    .Select(d => range.From.AddDays(d))
                    .Select(day => new DailyRow
                    {
                        Day = day,
                        Orders = sales.Count(o => o.LocalDate == day),
                        Revenue = sales.Where(o => o.LocalDate == day).Sum(o => o.Total)
                    })
                    .ToList(),

                TopProducts = items
                    .GroupBy(i => i.ProductName)
                    .Select(g => new NameAmountRow { Name = g.Key, Count = g.Sum(i => i.Quantity), Amount = g.Sum(i => i.LineTotal) })
                    .OrderByDescending(r => r.Amount).Take(10).ToList(),

                ByCategory = items
                    .GroupBy(i => i.CategoryName ?? "Unknown")
                    .Select(g => new NameAmountRow { Name = g.Key, Count = g.Sum(i => i.Quantity), Amount = g.Sum(i => i.LineTotal) })
                    .OrderByDescending(r => r.Amount).ToList(),

                ByPaymentMethod = sales
                    .GroupBy(o => o.PaymentMethod == PaymentMethod.CashOnDelivery ? "Cash on Delivery" : o.PaymentMethod == null ? "Unknown" : "Online")
                    .Select(g => new NameAmountRow { Name = g.Key, Count = g.Count(), Amount = g.Sum(o => o.Total) })
                    .OrderByDescending(r => r.Amount).ToList(),

                ByCity = sales
                    .GroupBy(o => string.IsNullOrWhiteSpace(o.City) ? "Unknown" : o.City.Trim())
                    .Select(g => new NameAmountRow { Name = g.Key, Count = g.Count(), Amount = g.Sum(o => o.Total) })
                    .OrderByDescending(r => r.Amount).Take(8).ToList()
            };

            return View(vm);
        }

        // GET: /Admin/Reports/Profit?from=2026-01-01&to=2026-12-31
        public async Task<IActionResult> Profit(DateTime? from, DateTime? to)
        {
            var range = MakeRange(from, to, nameof(Profit));
            var sales = (await LoadOrdersAsync(range)).Where(o => o.CountsAsSale).ToList();
            var expenses = await _context.Set<Expense>()
                .Where(e => e.Date >= range.From && e.Date <= range.To)
                .ToListAsync();

            var vm = new ProfitReportViewModel
            {
                Range = range,
                ProductSales = sales.Sum(o => o.SubTotal),
                Discounts = sales.Sum(o => o.Discount),
                Cogs = sales.SelectMany(o => o.Items).Sum(i => i.LineCost),
                ShippingIncome = sales.Sum(o => o.ShippingCost),
                TotalExpenses = expenses.Sum(e => e.Amount),
                ItemsWithoutCost = sales.SelectMany(o => o.Items).Count(i => i.CostEach == null),
                ExpensesByCategory = expenses
                    .GroupBy(e => e.Category)
                    .Select(g => new NameAmountRow { Name = ReportDisplay.Label(g.Key), Count = g.Count(), Amount = g.Sum(e => e.Amount) })
                    .OrderByDescending(r => r.Amount).ToList()
            };

            // Month-by-month breakdown
            var firstMonth = new DateTime(range.From.Year, range.From.Month, 1);
            for (var m = firstMonth; m <= range.To; m = m.AddMonths(1))
            {
                var monthSales = sales.Where(o => o.LocalDate.Year == m.Year && o.LocalDate.Month == m.Month).ToList();
                vm.Monthly.Add(new ProfitRow
                {
                    Label = m.ToString("MMM yyyy"),
                    NetSales = monthSales.Sum(o => o.SubTotal - o.Discount),
                    Cogs = monthSales.SelectMany(o => o.Items).Sum(i => i.LineCost),
                    ShippingIncome = monthSales.Sum(o => o.ShippingCost),
                    Expenses = expenses.Where(e => e.Date.Year == m.Year && e.Date.Month == m.Month).Sum(e => e.Amount)
                });
            }

            return View(vm);
        }

        // GET: /Admin/Reports/ExportSales?from=...&to=...   (CSV file, opens in Excel)
        public async Task<IActionResult> ExportSales(DateTime? from, DateTime? to)
        {
            var range = MakeRange(from, to, nameof(Sales));
            var orders = await LoadOrdersAsync(range);

            var csv = new StringBuilder();
            csv.AppendLine("Order number,Date,Customer,City,Status,Payment method,Payment status,Items,Subtotal,Shipping,Discount,Total,Counts as sale");
            foreach (var o in orders.OrderBy(o => o.OrderDate))
            {
                csv.AppendLine(string.Join(",",
                    Csv(o.OrderNumber),
                    o.OrderDate.AddHours(6).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    Csv(o.CustomerName),
                    Csv(o.City),
                    o.Status,
                    o.PaymentMethod?.ToString() ?? "",
                    o.PaymentStatus?.ToString() ?? "",
                    o.Items.Sum(i => i.Quantity),
                    o.SubTotal.ToString("0.00", CultureInfo.InvariantCulture),
                    o.ShippingCost.ToString("0.00", CultureInfo.InvariantCulture),
                    o.Discount.ToString("0.00", CultureInfo.InvariantCulture),
                    o.Total.ToString("0.00", CultureInfo.InvariantCulture),
                    o.CountsAsSale ? "Yes" : "No"));
            }

            // UTF-8 with BOM so Excel shows Bangla text correctly
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"sales_{range.From:yyyyMMdd}_{range.To:yyyyMMdd}.csv");
        }

        // ---------- Helpers ----------

        // Default: this month (Bangladesh time)
        private static DateRangeViewModel MakeRange(DateTime? from, DateTime? to, string action)
        {
            var todayBd = DateTime.UtcNow.AddHours(6).Date;
            var f = (from ?? new DateTime(todayBd.Year, todayBd.Month, 1)).Date;
            var t = (to ?? todayBd).Date;
            if (t < f) (f, t) = (t, f);
            if ((t - f).TotalDays > 366 * 3) f = t.AddYears(-3); // keep reports fast
            return new DateRangeViewModel { From = f, To = t, Action = action };
        }

        private async Task<List<ReportOrder>> LoadOrdersAsync(DateRangeViewModel range)
        {
            var fromUtc = range.From.AddHours(-6);
            var toUtc = range.To.AddDays(1).AddHours(-6);

            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.OrderDate >= fromUtc && o.OrderDate < toUtc)
                .Select(o => new ReportOrder
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    CustomerName = o.ShippingFullName,
                    City = o.ShippingCity,
                    SubTotal = o.SubTotal,
                    ShippingCost = o.ShippingCost,
                    Discount = o.Discount,
                    Total = o.Total,
                    PaymentMethod = o.Payments.OrderByDescending(p => p.Id).Select(p => (PaymentMethod?)p.Method).FirstOrDefault(),
                    PaymentStatus = o.Payments.OrderByDescending(p => p.Id).Select(p => (PaymentStatus?)p.Status).FirstOrDefault(),
                    Items = o.Items.Select(i => new ReportItem
                    {
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        LineTotal = i.LineTotal,
                        UnitCost = i.UnitCost,
                        CategoryName = i.ProductVariant != null && i.ProductVariant.Product != null && i.ProductVariant.Product.Category != null
                            ? i.ProductVariant.Product.Category.Name : null,
                        CurrentCost = i.ProductVariant != null && i.ProductVariant.Product != null
                            ? i.ProductVariant.Product.CostPrice : null
                    }).ToList()
                })
                .ToListAsync();
        }

        private static string Csv(string? value)
        {
            value ??= "";
            return value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }
    }
}
