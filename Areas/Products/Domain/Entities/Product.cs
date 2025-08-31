using Omni_MVC_2.Utilities.RepositoryUtilities;

namespace Omni_MVC_2.Areas.Products.Domain.Entities
{
    public class Product : Base<string>
    {
        // Columns
        public int IntCode { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }

        // Navigation
        public ICollection<Product_Document> Product_Documents { get; set; } = [];
    }
}
