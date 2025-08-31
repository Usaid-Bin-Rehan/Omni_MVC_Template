using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Omni_MVC_2.Areas.Products.Domain.Entities;

namespace Omni_MVC_2.Areas.Products.Domain.Relations
{
    public class Product_Config : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.Property(x => x.IntCode).HasDefaultValueSql("nextval('\"ProductSeries\"')");

            builder.HasMany(x => x.Product_Documents).WithOne(x => x.Product).HasForeignKey(x => x.ProductId);


        }
    }
}
