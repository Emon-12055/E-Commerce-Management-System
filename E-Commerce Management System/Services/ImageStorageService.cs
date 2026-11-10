namespace ECommerceManagementSystem.Services
{
    public interface IImageStorageService
    {
        /// <summary>Returns an error message if the file is not acceptable, otherwise null.</summary>
        string? Validate(IFormFile file);

        /// <summary>Saves the file under wwwroot/images/products and returns its URL, e.g. /images/products/abc.jpg</summary>
        Task<string> SaveProductImageAsync(IFormFile file);

        void DeleteImage(string? imageUrl);
    }

    public class ImageStorageService : IImageStorageService
    {
        private const string ProductFolder = "images/products";
        private const long MaxBytes = 5 * 1024 * 1024; // 5 MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        private readonly IWebHostEnvironment _env;

        public ImageStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public string? Validate(IFormFile file)
        {
            if (file == null || file.Length == 0) return "The selected file is empty.";
            if (file.Length > MaxBytes) return $"\"{file.FileName}\" is larger than 5 MB.";

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return $"\"{file.FileName}\" is not allowed. Use JPG, PNG, WEBP or GIF.";

            return null;
        }

        public async Task<string> SaveProductImageAsync(IFormFile file)
        {
            var error = Validate(file);
            if (error != null) throw new InvalidOperationException(error);

            var folder = Path.Combine(_env.WebRootPath, "images", "products");
            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
            var fullPath = Path.Combine(folder, fileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/{ProductFolder}/{fileName}";
        }

        public void DeleteImage(string? imageUrl)
        {
            // Only delete files that this service created
            if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith($"/{ProductFolder}/")) return;

            var fileName = Path.GetFileName(imageUrl);
            var fullPath = Path.Combine(_env.WebRootPath, "images", "products", fileName);
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
    }
}
