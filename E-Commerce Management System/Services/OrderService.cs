using ECommerceManagementSystem.Data;
using ECommerceManagementSystem.Models;
using ECommerceManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ECommerceManagementSystem.Services
{
    public record PlaceOrderResult(bool Success, Order? Order, string? Error);

    public interface IOrderService
    {
        decimal CalculateShipping(string? city);
        Task<PlaceOrderResult> PlaceOrderAsync(string userId, CheckoutViewModel form);
        Task<(bool Success, string Message)> CancelOrderAsync(int orderId, string? userId, string actingUserId, string reason);
        string NewTransactionId();
    }

    public class OrderService : IOrderService
    {
        // ----- Shipping charges (change these to your own rates)
        public const decimal ShippingInsideDhaka = 60m;
        public const decimal ShippingOutsideDhaka = 120m;

        private readonly ApplicationDbContext _db;
        private readonly ICartService _cart;

        public OrderService(ApplicationDbContext db, ICartService cart)
        {
            _db = db;
            _cart = cart;
        }

        public decimal CalculateShipping(string? city)
        {
            if (string.IsNullOrWhiteSpace(city)) return ShippingOutsideDhaka;
            return city.Trim().Equals("Dhaka", StringComparison.OrdinalIgnoreCase) ? ShippingInsideDhaka : ShippingOutsideDhaka;
        }

        public string NewTransactionId() => $"TXN{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

        // Turns the user's cart into an order: checks stock, saves the order,
        // reduces stock, logs inventory and empties the cart. All in one transaction.
        public async Task<PlaceOrderResult> PlaceOrderAsync(string userId, CheckoutViewModel form)
        {
            var cart = await _cart.GetCartAsync(userId);
            if (cart == null || !cart.Items.Any())
                return new PlaceOrderResult(false, null, "Your cart is empty.");

            // ----- 1. Check every item is still available
            foreach (var item in cart.Items)
            {
                var v = item.ProductVariant;
                if (v == null || !v.IsActive || v.Product == null || !v.Product.IsActive)
                    return new PlaceOrderResult(false, null, "An item in your cart is no longer available. Please check your cart.");

                if (v.StockQuantity < item.Quantity)
                    return new PlaceOrderResult(false, null,
                        $"Only {v.StockQuantity} left of {v.Product.Name} ({v.Size} / {v.Color}). Please update your cart.");
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // ----- 2. Create the order
                var subTotal = cart.Items.Sum(i => i.ProductVariant!.EffectivePrice * i.Quantity);
                var shipping = CalculateShipping(form.City);

                var order = new Order
                {
                    OrderNumber = await NewOrderNumberAsync(),
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    Status = OrderStatus.Pending,
                    ShippingFullName = form.FullName.Trim(),
                    ShippingPhone = form.Phone.Trim(),
                    ShippingAddressLine = form.Address.Trim(),
                    ShippingCity = form.City.Trim(),
                    ShippingPostalCode = form.PostalCode?.Trim(),
                    Notes = form.Notes?.Trim(),
                    SubTotal = subTotal,
                    ShippingCost = shipping,
                    Discount = 0,
                    Total = subTotal + shipping
                };

                // ----- 3. Copy each cart item into the order and reduce stock
                foreach (var item in cart.Items)
                {
                    var v = item.ProductVariant!;
                    var p = v.Product!;
                    var unitPrice = v.EffectivePrice;

                    order.Items.Add(new OrderItem
                    {
                        ProductVariantId = v.Id,
                        ProductName = p.Name,
                        SKU = v.SKU,
                        Size = v.Size,
                        Color = v.Color,
                        ImageUrl = p.Images.OrderByDescending(i => i.IsMain).ThenBy(i => i.Id).FirstOrDefault()?.ImageUrl,
                        UnitPrice = unitPrice,
                        Quantity = item.Quantity,
                        LineTotal = unitPrice * item.Quantity
                    });

                    v.StockQuantity -= item.Quantity;

                    _db.Set<InventoryTransaction>().Add(new InventoryTransaction
                    {
                        ProductVariantId = v.Id,
                        ChangeType = InventoryChangeType.Sale,
                        QuantityChange = -item.Quantity,
                        StockAfter = v.StockQuantity,
                        Order = order,
                        Note = $"Sold in order {order.OrderNumber}",
                        CreatedByUserId = userId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // ----- 4. Payment record
                bool online = form.PaymentMethod != PaymentMethod.CashOnDelivery;
                order.Payments.Add(new Payment
                {
                    Method = form.PaymentMethod,
                    Status = PaymentStatus.Pending,
                    Amount = order.Total,
                    Currency = "BDT",
                    TransactionId = online ? NewTransactionId() : null,
                    CreatedAt = DateTime.UtcNow
                });

                _db.Orders.Add(order);

                // ----- 5. Empty the cart
                _db.Set<CartItem>().RemoveRange(cart.Items);

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return new PlaceOrderResult(true, order, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone else bought the same item at the same moment (RowVersion changed)
                await transaction.RollbackAsync();
                return new PlaceOrderResult(false, null, "Stock changed while you were checking out. Please review your cart and try again.");
            }
        }

        // Cancels an order and puts the items back in stock.
        // userId = the customer's id (only their own order), or null for Admin/Staff.
        public async Task<(bool Success, string Message)> CancelOrderAsync(int orderId, string? userId, string actingUserId, string reason)
        {
            var order = await _db.Orders
                .Include(o => o.Items).ThenInclude(i => i.ProductVariant)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId && (userId == null || o.UserId == userId));

            if (order == null) return (false, "Order not found.");

            bool customer = userId != null;
            bool allowed = customer
                ? order.Status is OrderStatus.Pending or OrderStatus.Confirmed
                : order.Status is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Processing;

            if (!allowed)
                return (false, $"This order is already {order.Status} and can no longer be cancelled.");

            foreach (var item in order.Items.Where(i => i.ProductVariant != null))
            {
                item.ProductVariant!.StockQuantity += item.Quantity;

                _db.Set<InventoryTransaction>().Add(new InventoryTransaction
                {
                    ProductVariantId = item.ProductVariant.Id,
                    ChangeType = InventoryChangeType.OrderCancelled,
                    QuantityChange = item.Quantity,
                    StockAfter = item.ProductVariant.StockQuantity,
                    OrderId = order.Id,
                    Note = $"Order {order.OrderNumber} cancelled: {reason}",
                    CreatedByUserId = actingUserId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            foreach (var payment in order.Payments.Where(p => p.Status == PaymentStatus.Pending))
                payment.Status = PaymentStatus.Failed;

            order.Status = OrderStatus.Cancelled;
            await _db.SaveChangesAsync();

            bool wasPaid = order.Payments.Any(p => p.Status == PaymentStatus.Paid);
            return (true, wasPaid
                ? $"Order {order.OrderNumber} cancelled. Your online payment will be refunded."
                : $"Order {order.OrderNumber} cancelled.");
        }

        private async Task<string> NewOrderNumberAsync()
        {
            string number;
            do
            {
                number = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10000, 99999)}";
            }
            while (await _db.Orders.AnyAsync(o => o.OrderNumber == number));
            return number;
        }
    }
}
