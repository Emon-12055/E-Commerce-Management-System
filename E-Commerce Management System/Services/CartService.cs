using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Services
{
    public record CartResult(bool Success, string Message);

    public interface ICartService
    {
        Task<Cart?> GetCartAsync(string userId);
        Task<int> GetItemCountAsync(string userId);
        Task<CartResult> AddAsync(string userId, int variantId, int quantity);
        Task<CartResult> UpdateQuantityAsync(string userId, int cartItemId, int quantity);
        Task RemoveAsync(string userId, int cartItemId);
        Task ClearAsync(string userId);
    }

    // Shopping cart stored in the database (Cart + CartItem tables), one cart per logged-in user.
    public class CartService : ICartService
    {
        private const int MaxPerItem = 100; // CartItem.Quantity has [Range(1, 100)]
        private readonly ApplicationDbContext _db;

        public CartService(ApplicationDbContext db)
        {
            _db = db;
        }

        private DbSet<Cart> Carts => _db.Set<Cart>();
        private DbSet<CartItem> CartItems => _db.Set<CartItem>();

        // Cart with items, variants, products and images (for the Cart page)
        public async Task<Cart?> GetCartAsync(string userId)
        {
            return await Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(v => v!.Product)
                            .ThenInclude(p => p!.Images)
                .FirstOrDefaultAsync(c => c.UserId == userId);
        }

        // Total number of pieces in the cart (for the navbar badge)
        public async Task<int> GetItemCountAsync(string userId)
        {
            return await CartItems
                .Where(i => i.Cart!.UserId == userId)
                .SumAsync(i => (int?)i.Quantity) ?? 0;
        }

        public async Task<CartResult> AddAsync(string userId, int variantId, int quantity)
        {
            if (quantity < 1) quantity = 1;

            var variant = await _db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId);

            if (variant == null || !variant.IsActive || variant.Product == null || !variant.Product.IsActive)
                return new CartResult(false, "Sorry, this product is not available.");

            int maxAllowed = Math.Min(variant.StockQuantity, MaxPerItem);
            if (maxAllowed <= 0)
                return new CartResult(false, "Sorry, this size and color is out of stock.");

            var cart = await GetOrCreateCartAsync(userId);
            var item = await CartItems.FirstOrDefaultAsync(i => i.CartId == cart.Id && i.ProductVariantId == variantId);

            int alreadyInCart = item?.Quantity ?? 0;
            if (alreadyInCart >= maxAllowed)
                return new CartResult(false, $"You already have the maximum available ({maxAllowed}) in your cart.");

            int newQuantity = alreadyInCart + quantity;
            string message = $"{variant.Product.Name} ({variant.Size} / {variant.Color}) added to your cart.";
            if (newQuantity > maxAllowed)
            {
                newQuantity = maxAllowed;
                message = $"Only {maxAllowed} available, so your cart now has {maxAllowed}.";
            }

            if (item == null)
            {
                CartItems.Add(new CartItem
                {
                    CartId = cart.Id,
                    ProductVariantId = variantId,
                    Quantity = newQuantity,
                    AddedAt = DateTime.UtcNow
                });
            }
            else
            {
                item.Quantity = newQuantity;
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return new CartResult(true, message);
        }

        public async Task<CartResult> UpdateQuantityAsync(string userId, int cartItemId, int quantity)
        {
            var item = await CartItems
                .Include(i => i.Cart)
                .Include(i => i.ProductVariant)
                .FirstOrDefaultAsync(i => i.Id == cartItemId && i.Cart!.UserId == userId);
            if (item == null) return new CartResult(false, "Item not found in your cart.");

            if (quantity <= 0)
            {
                CartItems.Remove(item);
                await _db.SaveChangesAsync();
                return new CartResult(true, "Item removed from your cart.");
            }

            int stock = item.ProductVariant?.StockQuantity ?? 0;
            int maxAllowed = Math.Min(stock, MaxPerItem);
            if (maxAllowed <= 0)
                return new CartResult(false, "This item is now out of stock. Please remove it.");

            string message = "Cart updated.";
            if (quantity > maxAllowed)
            {
                quantity = maxAllowed;
                message = $"Only {maxAllowed} available, so the quantity was set to {maxAllowed}.";
            }

            item.Quantity = quantity;
            item.Cart!.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return new CartResult(true, message);
        }

        public async Task RemoveAsync(string userId, int cartItemId)
        {
            var item = await CartItems.FirstOrDefaultAsync(i => i.Id == cartItemId && i.Cart!.UserId == userId);
            if (item == null) return;

            CartItems.Remove(item);
            await _db.SaveChangesAsync();
        }

        public async Task ClearAsync(string userId)
        {
            var items = await CartItems.Where(i => i.Cart!.UserId == userId).ToListAsync();
            if (items.Count == 0) return;

            CartItems.RemoveRange(items);
            await _db.SaveChangesAsync();
        }

        private async Task<Cart> GetOrCreateCartAsync(string userId)
        {
            var cart = await Carts.FirstOrDefaultAsync(c => c.UserId == userId);
            if (cart != null) return cart;

            cart = new Cart { UserId = userId, CreatedAt = DateTime.UtcNow };
            Carts.Add(cart);
            await _db.SaveChangesAsync();
            return cart;
        }
    }
}
