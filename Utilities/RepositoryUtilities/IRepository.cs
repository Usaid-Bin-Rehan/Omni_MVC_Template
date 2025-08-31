using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;


namespace Omni_MVC_2.Utilities.RepositoryUtilities

{

    public interface IRepository<TEntity, PrimitiveType> where TEntity : Base<PrimitiveType>
    {
        SetterResult Add(TEntity entity, string createdBy);
        Task<SetterResult> AddAsync(TEntity entity, string createdBy, CancellationToken cancellationToken);

        SetterResult Update(TEntity entity, string updatedBy);
        Task<SetterResult> UpdateAsync(TEntity entity, string updatedBy, CancellationToken cancellationToken);
        SetterResult UpdateMany(TEntity[] entity);
        SetterResult UpdateOnCondition(Expression<Func<TEntity, bool>> filter, Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setPropertyCalls);
        Task<SetterResult> UpdateOnConditionAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setPropertyCalls, CancellationToken cancellationToken);

        SetterResult Delete(TEntity entity);
        Task<SetterResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken);
        SetterResult Delete(PrimitiveType id);
        Task<SetterResult> DeleteAsync(PrimitiveType id, CancellationToken cancellationToken);
        Task<SetterResult> DeleteRangeAsync(TEntity[] entities, CancellationToken cancellationToken);

        GetterResult<IEnumerable<TEntity>> Get(Expression<Func<TEntity, bool>> filter, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy, string includeProperties = "");
        Task<GetterResult<IEnumerable<TEntity>>> GetAsync(CancellationToken cancellationToken, Expression<Func<TEntity, bool>> filter = null!, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null!, string includeProperties = "");
        GetterResult<TEntity> GetSingle(Expression<Func<TEntity, bool>> filter, string includeProperties = "");
        Task<GetterResult<TEntity>> GetSingleAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken, string includeProperties = "");
        GetterResult<TEntity> GetById(PrimitiveType id);
        Task<GetterResult<TEntity>> GetByIdAsync(PrimitiveType id, CancellationToken cancellationToken);
        GetterResult<IEnumerable<TEntity>> GetAll();
        Task<GetterResult<IEnumerable<TEntity>>> GetAllAsync(CancellationToken cancellationToken);
        GetterResult<IQueryable<TEntity>> GetQueryable();

        GetterResult<bool> Any(Expression<Func<TEntity, bool>> filter);
        Task<GetterResult<bool>> AnyAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken);
        GetterResult<bool> All(Expression<Func<TEntity, bool>> filter);
    }
}