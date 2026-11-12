using ECommerceManagementSystem.Models;

namespace ECommerceManagementSystem.ViewModels
{
    // ---------- Raw data loaded once for a date range ----------
    public class ReportOrder
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }          // UTC
        public DateTime LocalDate => OrderDate.AddHours(6).Date; // Bangladesh date
        public OrderStatus Status { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
        public List<ReportItem> Items { get; set; } = new();

        // Cancelled and returned orders are not counted as sales
        public bool CountsAsSale => Status != OrderStatus.Cancelled && Status != OrderStatus.Returned;
    }

    public class ReportItem
    {
        public string ProductName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
        public decimal? UnitCost { get; set; }      // saved when the order was placed
        public decimal? CurrentCost { get; set; }   // product's cost price today (fallback for old orders)

        public decimal? CostEach => UnitCost ?? CurrentCost;
        public decimal LineCost => (CostEach ?? 0) * Quantity;
    }

    // ---------- Shared ----------
    public class DateRangeViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Action { get; set; } = "Sales";
    }

    public class NameAmountRow
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    public class DailyRow
    {
        public DateTime Day { get; set; }
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
    }

    // ---------- Sales report ----------
    public class SalesReportViewModel
    {
        public DateRangeViewModel Range { get; set; } = new();

        public int OrderCount { get; set; }
        public int ItemsSold { get; set; }
        public decimal ProductSales { get; set; }
        public decimal ShippingIncome { get; set; }
        public decimal Discounts { get; set; }
        public decimal Revenue { get; set; }
        public decimal AverageOrderValue => OrderCount == 0 ? 0 : Revenue / OrderCount;

        public int CancelledCount { get; set; }
        public decimal CancelledValue { get; set; }
        public int ReturnedCount { get; set; }

        public List<DailyRow> Daily { get; set; } = new();
        public List<NameAmountRow> TopProducts { get; set; } = new();
        public List<NameAmountRow> ByCategory { get; set; } = new();
        public List<NameAmountRow> ByPaymentMethod { get; set; } = new();
        public List<NameAmountRow> ByCity { get; set; } = new();
    }

    // ---------- Profit report ----------
    public class ProfitRow
    {
        public string Label { get; set; } = string.Empty;
        public decimal NetSales { get; set; }
        public decimal Cogs { get; set; }
        public decimal ShippingIncome { get; set; }
        public decimal Expenses { get; set; }
        public decimal GrossProfit => NetSales - Cogs;
        public decimal NetProfit => GrossProfit + ShippingIncome - Expenses;
    }

    public class ProfitReportViewModel
    {
        public DateRangeViewModel Range { get; set; } = new();

        public decimal ProductSales { get; set; }
        public decimal Discounts { get; set; }
        public decimal NetSales => ProductSales - Discounts;
        public decimal Cogs { get; set; }                     // cost of goods sold
        public decimal GrossProfit => NetSales - Cogs;
        public decimal GrossMargin => NetSales == 0 ? 0 : GrossProfit / NetSales * 100;
        public decimal ShippingIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit => GrossProfit + ShippingIncome - TotalExpenses;
        public decimal NetMargin => (NetSales + ShippingIncome) == 0 ? 0 : NetProfit / (NetSales + ShippingIncome) * 100;

        public int ItemsWithoutCost { get; set; }
        public List<NameAmountRow> ExpensesByCategory { get; set; } = new();
        public List<ProfitRow> Monthly { get; set; } = new();
    }

    // ---------- Expenses page ----------
    public class ExpenseListViewModel
    {
        public DateRangeViewModel Range { get; set; } = new();
        public ExpenseCategory? Category { get; set; }
        public List<Expense> Expenses { get; set; } = new();
        public decimal Total => Expenses.Sum(e => e.Amount);
        public List<NameAmountRow> ByCategory { get; set; } = new();
    }

    public static class ReportDisplay
    {
        public static string Label(ExpenseCategory c) => c switch
        {
            ExpenseCategory.InternetAndSoftware => "Internet & software",
            ExpenseCategory.PaymentFees => "Payment gateway fees",
            _ => c.ToString()
        };
    }
}
