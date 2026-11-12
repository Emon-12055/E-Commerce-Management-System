using System.ComponentModel.DataAnnotations;
using ECommerceManagementSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ECommerceManagementSystem.ViewModels
{
    // ---------- Dashboard ----------
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalOrders { get; set; }
        public int TotalStaff { get; set; }
        public int TotalCustomers { get; set; }
        public int LowStockThreshold { get; set; }
        public List<ProductVariant> LowStockItems { get; set; } = new();

        // Phase 7: order stats
        public int PendingOrders { get; set; }
        public int ToPackOrders { get; set; }      // Confirmed + Processing
        public int ShippedOrders { get; set; }
        public int TodayOrders { get; set; }
        public decimal TodaySales { get; set; }
        public decimal MonthSales { get; set; }
        public List<Order> RecentOrders { get; set; } = new();
    }

    // Phase 7: one row in the staff order list
    public class AdminOrderRowViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal Total { get; set; }
        public OrderStatus Status { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
    }

    // Phase 7: one row in stock history
    public class InventoryHistoryRowViewModel
    {
        public DateTime CreatedAt { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public InventoryChangeType ChangeType { get; set; }
        public int QuantityChange { get; set; }
        public int StockAfter { get; set; }
        public string? OrderNumber { get; set; }
        public int? OrderId { get; set; }
        public string? Note { get; set; }
        public string? UserName { get; set; }
    }

    // ---------- Categories ----------
    public class CategoryFormViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Display only (Index page)
        public int ProductCount { get; set; }
    }

    // ---------- Products ----------
    public class ProductListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public string? MainImageUrl { get; set; }
        public int TotalStock { get; set; }
        public int VariantCount { get; set; }
    }

    public class ProductFormViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required, Range(0.01, 10000000, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }

        [Range(0, 10000000, ErrorMessage = "Cost price cannot be negative.")]
        [Display(Name = "Cost price (what you paid)")]
        public decimal? CostPrice { get; set; }

        [Required(ErrorMessage = "Please choose a category.")]
        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        [Display(Name = "Active (visible in the shop)")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Upload images")]
        public List<IFormFile> NewImages { get; set; } = new();

        // Filled by the controller, not posted by the form
        public IEnumerable<SelectListItem> Categories { get; set; } = Enumerable.Empty<SelectListItem>();
        public List<ProductImage> ExistingImages { get; set; } = new();
        public List<ProductVariant> Variants { get; set; } = new();
    }

    public class VariantFormViewModel
    {
        [Required]
        public int ProductId { get; set; }

        [Required, StringLength(20)]
        public string Size { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string Color { get; set; } = string.Empty;

        [Range(0, 100000)]
        [Display(Name = "Stock")]
        public int StockQuantity { get; set; }
    }
}
