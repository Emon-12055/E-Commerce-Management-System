using System;
using System.Linq;
using System.Threading.Tasks;

using ECommerceManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceManagementSystem.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await db.Database.MigrateAsync();

            // ----- Roles
            // ----- Roles
            foreach (var role in AppRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // ----- Admin user
            const string adminEmail = "admin@shop.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Administrator",
                    EmailConfirmed = true,
                    IsActive = true
                };


                var result = await userManager.CreateAsync(admin, "Admin@12345");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }

            // ----- Stop if products already exist
            if (await db.Categories.AnyAsync()) return;

            // ----- Categories
            var men = new Category { Name = "Men", Slug = "men", DisplayOrder = 1 };
            var women = new Category { Name = "Women", Slug = "women", DisplayOrder = 2 };
            var menTshirts = new Category { Name = "T-Shirts", Slug = "men-tshirts", ParentCategory = men };
            var menJeans = new Category { Name = "Jeans", Slug = "men-jeans", ParentCategory = men };

            db.Categories.AddRange(men, women, menTshirts, menJeans);

            // ----- Product 1: T-Shirt
            var tshirt = new Product
            {
                Name = "Classic Cotton T-Shirt",
                Slug = "classic-cotton-tshirt",
                Description = "100% cotton, regular fit.",
                Brand = "Basics",
                BasePrice = 650m,
                CompareAtPrice = 850m,
                IsFeatured = true,
                Category = menTshirts
            };

            var colors = new[]
            {
                new { Name = "Black", Hex = "#000000", Code = "BLK" },
                new { Name = "Navy",  Hex = "#1E3A8A", Code = "NVY" },
                new { Name = "White", Hex = "#FFFFFF", Code = "WHT" }
            };
            string[] sizes = { "S", "M", "L", "XL" };

            foreach (var color in colors)
            {
                foreach (var size in sizes)
                {
                    tshirt.Variants.Add(new ProductVariant
                    {
                        SKU = "TSH-" + color.Code + "-" + size,
                        Size = size,
                        Color = color.Name,
                        ColorHex = color.Hex,
                        StockQuantity = 20,
                        Price = size == "XL" ? 700m : (decimal?)null
                    });
                }

                tshirt.Images.Add(new ProductImage
                {
                    ImageUrl = "/images/products/tshirt-" + color.Name.ToLower() + ".jpg",
                    AltText = "T-Shirt " + color.Name,
                    Color = color.Name,
                    IsMain = color.Name == "Black"
                });
            }

            // ----- Product 2: Jeans
            var jeans = new Product
            {
                Name = "Slim Fit Denim Jeans",
                Slug = "slim-fit-denim-jeans",
                Description = "Stretch denim, slim fit.",
                Brand = "Denimco",
                BasePrice = 1850m,
                Category = menJeans
            };

            string[] jeanSizes = { "30", "32", "34", "36" };
            foreach (var size in jeanSizes)
            {
                jeans.Variants.Add(new ProductVariant
                {
                    SKU = "JNS-BLU-" + size,
                    Size = size,
                    Color = "Blue",
                    ColorHex = "#2563EB",
                    StockQuantity = 15
                });
            }

            jeans.Images.Add(new ProductImage
            {
                ImageUrl = "/images/products/jeans-blue.jpg",
                AltText = "Slim Fit Jeans",
                Color = "Blue",
                IsMain = true
            });

            db.Products.AddRange(tshirt, jeans);
            await db.SaveChangesAsync();

            // ----- Opening stock log
            var variants = await db.ProductVariants.ToListAsync();
            foreach (var v in variants)
            {
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductVariantId = v.Id,
                    ChangeType = InventoryChangeType.StockIn,
                    QuantityChange = v.StockQuantity,
                    StockAfter = v.StockQuantity,
                    Note = "Opening stock"
                });
            }
            await db.SaveChangesAsync();
        }
    }
}