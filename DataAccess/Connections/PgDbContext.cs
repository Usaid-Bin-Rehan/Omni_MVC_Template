using Microsoft.EntityFrameworkCore;
using Omni_MVC_2.Areas.Products.Domain.Entities;
using System.Reflection;

namespace Omni_MVC_2.DataAccess.Connections
{
    public class PgDbContext : DbContext
    {
        public PgDbContext(DbContextOptions<PgDbContext> dbContextOptions) : base(dbContextOptions) { }

        //public DbSet<Product> Product { get; set; }
        //public DbSet<Product_Document> Product_Document {get; set; }

        #region OnModelCreating
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            SeedDocumentNumbering(builder);
        }

        private static void SeedDocumentNumbering(ModelBuilder builder)
        {
            //builder.HasSequence<int>("ProductSeries").StartsAt(100000).IncrementsBy(1);

        }
        #endregion OnModelCreating

        #region SaveChanges
        private void ConvertDateTimesToUtc()
        {
            IEnumerable<object>? entities = ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified).Select(e => e.Entity);
            foreach (var entity in entities)
            {
                IEnumerable<PropertyInfo>? properties = entity.GetType().GetProperties().Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?));
                foreach (PropertyInfo? property in properties)
                {
                    if (property.GetValue(entity) is DateTime dateTime) property.SetValue(entity, dateTime.ToUniversalTime());
                }
            }
        }

        public override int SaveChanges()
        {
            ConvertDateTimesToUtc();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ConvertDateTimesToUtc();
            return await base.SaveChangesAsync(cancellationToken);
        }
        #endregion SaveChanges
    }
}