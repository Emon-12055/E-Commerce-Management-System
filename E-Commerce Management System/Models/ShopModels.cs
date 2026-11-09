using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.AspNetCore.Identity;

namespace ECommerceManagementSystem.Models
{
    // =========================================================
    // USER + ROLES
    // =========================================================
    public class ApplicationUser : IdentityUser
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Staff = "Staff";
        public const string Customer = "Customer";
        public const string AdminOrStaff = "Admin,Staff";

        public static readonly string[] All = { Admin, Staff, Customer };
    }

    // =========================================================
    // ENUMS
    // =========================================================
    public enum OrderStatus
    {
        Pending = 0, Confirmed = 1, Processing = 2, Shipped = 3,
        Delivered = 4, Cancelled = 5, Returned = 6
    }

    public enum PaymentStatus
    {
        Pending = 0, Paid = 1, Failed = 2, Refunded = 3
    }

    public enum PaymentMethod
    {
        CashOnDelivery = 0, Card = 1, BKash = 2, Nagad = 3, SSLCommerz = 4
    }

    public enum InventoryChangeType
    {
        StockIn = 0, Sale = 1, Return = 2, Adjustment = 3, OrderCancelled = 4
    }

    // =========================================================
    // CATEGORY
    // =========================================================
    public class Category
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(120)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        public int? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }
        public ICollection<Category> SubCategories { get; set; } = new List<Category>();

        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    // =========================================================
    // PRODUCT
    // =========================================================
    public class Product
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(220)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Brand { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 9999999)]
        public decimal BasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CompareAtPrice { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [Required]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

        [NotMapped]
        public int TotalStock => Variants.Sum(v => v.StockQuantity);
    }

    // =========================================================
    // PRODUCT VARIANT (size + color)
    // =========================================================
    public class ProductVariant
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required, StringLength(50)]
        public string SKU { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string Size { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string Color { get; set; } = string.Empty;

        [StringLength(7)]
        public string? ColorHex { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Price { get; set; }

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        public int LowStockThreshold { get; set; } = 5;
        public bool IsActive { get; set; } = true;

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

        [NotMapped]
        public decimal EffectivePrice => Price ?? Product?.BasePrice ?? 0m;

        [NotMapped]
        public bool InStock => StockQuantity > 0;
    }

    // =========================================================
    // PRODUCT IMAGE
    // =========================================================
    public class ProductImage
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required, StringLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [StringLength(200)]
        public string? AltText { get; set; }

        [StringLength(30)]
        public string? Color { get; set; }

        public bool IsMain { get; set; }
        public int DisplayOrder { get; set; }
    }

    // =========================================================
    // CART
    // =========================================================
    public class Cart
    {
        public int Id { get; set; }

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [StringLength(100)]
        public string? SessionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

        [NotMapped]
        public decimal SubTotal => Items.Sum(i => i.LineTotal);

        [NotMapped]
        public int TotalItems => Items.Sum(i => i.Quantity);
    }

    public class CartItem
    {
        public int Id { get; set; }

        [Required]
        public int CartId { get; set; }
        public Cart? Cart { get; set; }

        [Required]
        public int ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; } = 1;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public decimal UnitPrice => ProductVariant?.EffectivePrice ?? 0m;

        [NotMapped]
        public decimal LineTotal => UnitPrice * Quantity;
    }

    // =========================================================
    // ORDER
    // =========================================================
    public class Order
    {
        public int Id { get; set; }

        [Required, StringLength(30)]
        public string OrderNumber { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Required, StringLength(100)]
        public string ShippingFullName { get; set; } = string.Empty;

        [Required, StringLength(20), Phone]
        public string ShippingPhone { get; set; } = string.Empty;

        [Required, StringLength(250)]
        public string ShippingAddressLine { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string ShippingCity { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ShippingPostalCode { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class OrderItem
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int? ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

        [Required, StringLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [StringLength(50)]
        public string SKU { get; set; } = string.Empty;

        [StringLength(20)]
        public string Size { get; set; } = string.Empty;

        [StringLength(30)]
        public string Color { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }
    }

    // =========================================================
    // PAYMENT
    // =========================================================
    public class Payment
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public PaymentMethod Method { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(10)]
        public string Currency { get; set; } = "BDT";

        [StringLength(100)]
        public string? TransactionId { get; set; }

        public string? GatewayResponse { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
    }

    // =========================================================
    // INVENTORY LOG
    // =========================================================
    public class InventoryTransaction
    {
        public int Id { get; set; }

        [Required]
        public int ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

        public InventoryChangeType ChangeType { get; set; }

        public int QuantityChange { get; set; }
        public int StockAfter { get; set; }

        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        [StringLength(300)]
        public string? Note { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}