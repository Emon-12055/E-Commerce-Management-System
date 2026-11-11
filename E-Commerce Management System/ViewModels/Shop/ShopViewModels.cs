using ECommerceManagementSystem.Models;

namespace ECommerceManagementSystem.ViewModels
{
    // One product card (used on Home page and Shop page)
    public class ProductCardViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? CategoryName { get; set; }
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public string? ImageUrl { get; set; }
        public bool InStock { get; set; }

        public bool OnSale => CompareAtPrice.HasValue && CompareAtPrice > Price;
        public int DiscountPercent => OnSale ? (int)Math.Round((1 - Price / CompareAtPrice!.Value) * 100) : 0;
    }

    // Home page
    public class HomeProductsViewModel
    {
        public List<Category> Categories { get; set; } = new();
        public List<ProductCardViewModel> Featured { get; set; } = new();
        public List<ProductCardViewModel> NewArrivals { get; set; } = new();
    }

    // Shop page (listing + search + filters)
    public class ShopIndexViewModel
    {
        // Filters chosen by the customer
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public bool InStockOnly { get; set; }
        public string Sort { get; set; } = "newest";

        // Paging
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        // Data for the page
        public List<ProductCardViewModel> Products { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public List<string> Sizes { get; set; } = new();
        public List<string> Colors { get; set; } = new();

        // Builds the link for another page while keeping the filters
        public Dictionary<string, string> RouteFor(int page)
        {
            var d = new Dictionary<string, string> { ["page"] = page.ToString(), ["sort"] = Sort };
            if (!string.IsNullOrWhiteSpace(Search)) d["search"] = Search;
            if (CategoryId.HasValue) d["categoryId"] = CategoryId.Value.ToString();
            if (MinPrice.HasValue) d["minPrice"] = MinPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (MaxPrice.HasValue) d["maxPrice"] = MaxPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(Size)) d["size"] = Size;
            if (!string.IsNullOrWhiteSpace(Color)) d["color"] = Color;
            if (InStockOnly) d["inStockOnly"] = "true";
            return d;
        }
    }

    // Product details page
    public class ProductDetailsViewModel
    {
        public Product Product { get; set; } = null!;
        public List<ProductImage> Images { get; set; } = new();
        public List<VariantOption> Variants { get; set; } = new();
        public List<string> Sizes { get; set; } = new();
        public List<string> Colors { get; set; } = new();
        public List<ProductCardViewModel> Related { get; set; } = new();
        public int TotalStock => Variants.Sum(v => v.Stock);
    }

    public class VariantOption
    {
        public int Id { get; set; }
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Stock { get; set; }
        public decimal Price { get; set; }
    }
}
