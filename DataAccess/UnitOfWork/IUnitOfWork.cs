namespace Omni_MVC_2.DataAccess.UnitOfWork
{
    public interface IUnitOfWork
    {
        //public IRepository<Product, string> Product { get; }
        //public IRepository<Product_Document, string> Product_Document { get; }

        void Commit();
        Task CommitAsync(CancellationToken cancellationToken);
        Task CommitAsync();
    }
}
