using Omni_MVC_2.Utilities.RepositoryUtilities;

namespace Omni_MVC_2.Areas.Products.Domain.Entities
{
    public class Product_Document : Base<string>
    {
        // Columns
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Url { get; set; }

        // FKs
        public string? ProductId { get; set; }

        // Navigation
        public Product? Product { get; set; }
    }
}
