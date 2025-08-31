using Microsoft.EntityFrameworkCore;
using Omni_MVC_2.Areas.Products.Domain.Entities;
using Omni_MVC_2.Utilities.RepositoryUtilities;

namespace Omni_MVC_2.Areas.Products.Domain.Repositories
{
    public class PgProductRepository : GenericRepository<Product, string>
    {
        public PgProductRepository(DbContext context) : base(context) { }

        //public override async Task<SetterResult> AddAsync(Product entity, string createdBy, CancellationToken ct)
        //{
        //    Console.WriteLine("Custom AddAsync logic for Product");
        //    var result = await base.AddAsync(entity, createdBy, ct);
        //    return result;
        //}
    }
}