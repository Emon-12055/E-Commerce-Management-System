
using ECommerceManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Must stay first – creates the Identity tables
            base.OnModelCreating(builder);

            // ---------- Category ----------
            builder.Entity<Category>(e =>
            {
                e.HasIndex(c => c.Slug).IsUnique();

                e.HasOne(c => c.ParentCategory)
                 .WithMany(c => c.SubCategories)
                 .HasForeignKey(c => c.ParentCategoryId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- Product ----------
            builder.Entity<Product>(e =>
            {
                e.HasIndex(p => p.Slug).IsUnique();
                e.HasIndex(p => p.Name);

                e.HasOne(p => p.Category)
                 .WithMany(c => c.Products)
                 .HasForeignKey(p => p.CategoryId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ---------- ProductVariant ----------
            builder.Entity<ProductVariant>(e =>
            {
                e.HasIndex(v => v.SKU).IsUnique();
                e.HasIndex(v => new { v.ProductId, v.Size, v.Color }).IsUnique();

                e.HasOne(v => v.Product)
                 .WithMany(p => p.Variants)
                 .HasForeignKey(v => v.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------- ProductImage ----------
            builder.Entity<ProductImage>(e =>
            {
                e.HasOne(i => i.Product)
                 .WithMany(p => p.Images)
                 .HasForeignKey(i => i.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------- Cart ----------
            builder.Entity<Cart>(e =>
            {
                e.HasIndex(c => c.UserId);
                e.HasIndex(c => c.SessionId);

                e.HasOne(c => c.User)
                 .WithMany()
                 .HasForeignKey(c => c.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------- CartItem ----------
            builder.Entity<CartItem>(e =>
            {
                e.HasIndex(i => new { i.CartId, i.ProductVariantId }).IsUnique();

                e.HasOne(i => i.Cart)
                 .WithMany(c => c.Items)
                 .HasForeignKey(i => i.CartId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(i => i.ProductVariant)
                 .WithMany()
                 .HasForeignKey(i => i.ProductVariantId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------- Order ----------
            builder.Entity<Order>(e =>
            {
                e.HasIndex(o => o.OrderNumber).IsUnique();
                e.HasIndex(o => o.UserId);
                e.HasIndex(o => o.Status);

                e.HasOne(o => o.User)
                 .WithMany()
                 .HasForeignKey(o => o.UserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(o => o.Status)
                 .HasConversion<string>()
                 .HasMaxLength(20);
            });

            // ---------- OrderItem ----------
            builder.Entity<OrderItem>(e =>
            {
                e.HasOne(i => i.Order)
                 .WithMany(o => o.Items)
                 .HasForeignKey(i => i.OrderId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(i => i.ProductVariant)
                 .WithMany()
                 .HasForeignKey(i => i.ProductVariantId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ---------- Payment ----------
            builder.Entity<Payment>(e =>
            {
                e.HasIndex(p => p.TransactionId);

                e.HasOne(p => p.Order)
                 .WithMany(o => o.Payments)
                 .HasForeignKey(p => p.OrderId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(p => p.Method)
                 .HasConversion<string>()
                 .HasMaxLength(20);

                e.Property(p => p.Status)
                 .HasConversion<string>()
                 .HasMaxLength(20);
            });

            // ---------- InventoryTransaction ----------
            builder.Entity<InventoryTransaction>(e =>
            {
                e.HasOne(t => t.ProductVariant)
                 .WithMany(v => v.InventoryTransactions)
                 .HasForeignKey(t => t.ProductVariantId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(t => t.Order)
                 .WithMany()
                 .HasForeignKey(t => t.OrderId)
                 .OnDelete(DeleteBehavior.NoAction);

                e.Property(t => t.ChangeType)
                 .HasConversion<string>()
                 .HasMaxLength(20);
            });
        }
    }
}