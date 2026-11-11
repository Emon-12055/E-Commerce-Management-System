using System.Globalization;
using System.Text.Json;
using ECommerceManagementSystem.Models;
using Microsoft.Extensions.Options;

namespace ECommerceManagementSystem.Services
{
    // Settings from appsettings.json -> "SSLCommerz": { "StoreId": "...", "StorePassword": "...", "IsSandbox": true }
    public class SslCommerzOptions
    {
        public string? StoreId { get; set; }
        public string? StorePassword { get; set; }
        public bool IsSandbox { get; set; } = true;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(StoreId) && !string.IsNullOrWhiteSpace(StorePassword);
        public string BaseUrl => IsSandbox ? "https://sandbox.sslcommerz.com" : "https://securepay.sslcommerz.com";
    }

    public record PaymentInitResult(bool Success, string? GatewayUrl, string? Error);

    public record PaymentValidationResult(bool IsValid, string? TransactionId, decimal Amount, string RawResponse);

    public interface ISslCommerzService
    {
        bool IsConfigured { get; }

        // Starts a payment and returns the SSLCommerz page URL to send the customer to
        Task<PaymentInitResult> InitiateAsync(Order order, Payment payment, ApplicationUser user, string siteBaseUrl);

        // Asks SSLCommerz whether a payment (val_id) is really valid
        Task<PaymentValidationResult> ValidateAsync(string valId);
    }

    // SSLCommerz is a Bangladeshi payment gateway (bKash, Nagad, Rocket, cards, internet banking).
    public class SslCommerzService : ISslCommerzService
    {
        private readonly HttpClient _http;
        private readonly SslCommerzOptions _options;
        private readonly ILogger<SslCommerzService> _logger;

        public SslCommerzService(HttpClient http, IOptions<SslCommerzOptions> options, ILogger<SslCommerzService> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task<PaymentInitResult> InitiateAsync(Order order, Payment payment, ApplicationUser user, string siteBaseUrl)
        {
            if (!IsConfigured) return new PaymentInitResult(false, null, "Online payment is not set up.");

            var form = new Dictionary<string, string>
            {
                ["store_id"] = _options.StoreId!,
                ["store_passwd"] = _options.StorePassword!,
                ["total_amount"] = payment.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                ["currency"] = "BDT",
                ["tran_id"] = payment.TransactionId!,
                ["success_url"] = $"{siteBaseUrl}/Payment/Success",
                ["fail_url"] = $"{siteBaseUrl}/Payment/Fail",
                ["cancel_url"] = $"{siteBaseUrl}/Payment/Cancel",
                ["ipn_url"] = $"{siteBaseUrl}/Payment/Ipn",

                ["cus_name"] = order.ShippingFullName,
                ["cus_email"] = user.Email ?? "customer@example.com",
                ["cus_add1"] = order.ShippingAddressLine,
                ["cus_city"] = order.ShippingCity,
                ["cus_postcode"] = order.ShippingPostalCode ?? "1000",
                ["cus_country"] = "Bangladesh",
                ["cus_phone"] = order.ShippingPhone,

                ["shipping_method"] = "NO",
                ["num_of_item"] = order.Items.Sum(i => i.Quantity).ToString(),
                ["product_name"] = string.Join(", ", order.Items.Select(i => i.ProductName)).Truncate(250),
                ["product_category"] = "Clothing",
                ["product_profile"] = "general",
                ["value_a"] = order.OrderNumber
            };

            try
            {
                var response = await _http.PostAsync($"{_options.BaseUrl}/gwprocess/v4/api.php", new FormUrlEncodedContent(form));
                var body = await response.Content.ReadAsStringAsync();

                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;
                var status = root.GetPropertyOrDefault("status");
                var gatewayUrl = root.GetPropertyOrDefault("GatewayPageURL");

                if (status == "SUCCESS" && !string.IsNullOrEmpty(gatewayUrl))
                    return new PaymentInitResult(true, gatewayUrl, null);

                var reason = root.GetPropertyOrDefault("failedreason") ?? "Unknown error";
                _logger.LogWarning("SSLCommerz init failed for {Order}: {Reason}", order.OrderNumber, reason);
                return new PaymentInitResult(false, null, $"Payment gateway error: {reason}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSLCommerz init error for {Order}", order.OrderNumber);
                return new PaymentInitResult(false, null, "Could not connect to the payment gateway. Please try again.");
            }
        }

        public async Task<PaymentValidationResult> ValidateAsync(string valId)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(valId))
                return new PaymentValidationResult(false, null, 0, "");

            var url = $"{_options.BaseUrl}/validator/api/validationserverAPI.php" +
                      $"?val_id={Uri.EscapeDataString(valId)}" +
                      $"&store_id={Uri.EscapeDataString(_options.StoreId!)}" +
                      $"&store_passwd={Uri.EscapeDataString(_options.StorePassword!)}" +
                      "&format=json";
            try
            {
                var body = await _http.GetStringAsync(url);
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;

                var status = root.GetPropertyOrDefault("status");
                var tranId = root.GetPropertyOrDefault("tran_id");
                decimal.TryParse(root.GetPropertyOrDefault("amount"), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount);

                bool valid = status == "VALID" || status == "VALIDATED";
                return new PaymentValidationResult(valid, tranId, amount, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSLCommerz validation error for val_id {ValId}", valId);
                return new PaymentValidationResult(false, null, 0, ex.Message);
            }
        }
    }

    internal static class SslCommerzExtensions
    {
        public static string? GetPropertyOrDefault(this JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        public static string Truncate(this string value, int max) => value.Length <= max ? value : value[..max];
    }
}
