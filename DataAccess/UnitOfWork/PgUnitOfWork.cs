using Omni_MVC_2.DataAccess.Connections;

namespace Omni_MVC_2.DataAccess.UnitOfWork
{
    public class PgUnitOfWork : IUnitOfWork
    {
        private readonly PgDbContext _db;
        public PgUnitOfWork(PgDbContext db) { _db = db; }

        //public IRepository<Product, string> Product => new PgProductRepository<Product, string>(_db);
        //public IRepository<Product_Document, string> Product_Document => new PgGenericRepository<Product_Document, string>(_db);

        #region Commit
        public void Commit()
        {
            _db.SaveChanges();
        }
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        public async Task CommitAsync()
        {
            await _db.SaveChangesAsync();
        }
        public virtual void Dispose()
        {
            _db.Dispose();
            GC.SuppressFinalize(this);
        }
        #endregion Commit
    }
}