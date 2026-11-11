using System.ComponentModel.DataAnnotations;
using ECommerceManagementSystem.Models;

namespace ECommerceManagementSystem.ViewModels
{
    // Checkout form
    public class CheckoutViewModel
    {
        [Required, StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, Phone, StringLength(20)]
        [Display(Name = "Phone number")]
        public string Phone { get; set; } = string.Empty;

        [Required, StringLength(250)]
        [Display(Name = "Address")]
        public string Address { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "Postal code (optional)")]
        public string? PostalCode { get; set; }

        [StringLength(500)]
        [Display(Name = "Order note (optional)")]
        public string? Notes { get; set; }

        [Required]
        [Display(Name = "Payment method")]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;

        // Filled by the controller (display only)
        public Cart? Cart { get; set; }
        public bool OnlinePaymentEnabled { get; set; }
    }

    // One row on the "My orders" page
    public class OrderListItemViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public OrderStatus Status { get; set; }
        public decimal Total { get; set; }
        public int ItemCount { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
    }
}

namespace ECommerceManagementSystem.ViewModels
{
    // Colors and labels for order/payment status badges (used in views)
    public static class OrderDisplay
    {
        public static string Badge(OrderStatus s) => s switch
        {
            OrderStatus.Pending => "bg-warning text-dark",
            OrderStatus.Confirmed => "bg-info text-dark",
            OrderStatus.Processing => "bg-primary",
            OrderStatus.Shipped => "bg-primary",
            OrderStatus.Delivered => "bg-success",
            OrderStatus.Cancelled => "bg-secondary",
            OrderStatus.Returned => "bg-dark",
            _ => "bg-light text-dark"
        };

        public static string Badge(PaymentStatus s) => s switch
        {
            PaymentStatus.Paid => "bg-success",
            PaymentStatus.Pending => "bg-warning text-dark",
            PaymentStatus.Failed => "bg-danger",
            PaymentStatus.Refunded => "bg-secondary",
            _ => "bg-light text-dark"
        };

        public static string Label(PaymentMethod m) => m switch
        {
            PaymentMethod.CashOnDelivery => "Cash on Delivery",
            PaymentMethod.SSLCommerz => "Online (SSLCommerz)",
            PaymentMethod.BKash => "bKash",
            PaymentMethod.Nagad => "Nagad",
            PaymentMethod.Card => "Card",
            _ => m.ToString()
        };

        // Dhaka time for display (database stores UTC)
        public static DateTime Local(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), BdTime);

        private static readonly TimeZoneInfo BdTime = FindBdTime();
        private static TimeZoneInfo FindBdTime()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka"); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Bangladesh Standard Time"); }
                catch { return TimeZoneInfo.CreateCustomTimeZone("BST", TimeSpan.FromHours(6), "BST", "BST"); }
            }
        }
    }
}
