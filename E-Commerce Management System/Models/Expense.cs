using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceManagementSystem.Models
{
    // Phase 8: business expenses (for the profit report)
    // Do NOT record buying stock here: product cost is already counted
    // from each product's Cost price (cost of goods sold).
    public enum ExpenseCategory
    {
        Rent = 0,
        Salary = 1,
        Marketing = 2,
        Delivery = 3,
        Packaging = 4,
        Utilities = 5,
        InternetAndSoftware = 6,
        PaymentFees = 7,
        Other = 9
    }

    public class Expense
    {
        public int Id { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.UtcNow.AddHours(6).Date;

        public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000000)]
        public decimal Amount { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
