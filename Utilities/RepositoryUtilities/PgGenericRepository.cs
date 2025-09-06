using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace Omni_MVC_2.Utilities.RepositoryUtilities
{
    public class GenericRepository<TEntity, PrimitiveType> : IRepository<TEntity, PrimitiveType> where TEntity : Base<PrimitiveType>
    {
        protected readonly DbContext _db;
        protected readonly DbSet<TEntity> _dbSet;
        private IDbContextTransaction? _currentTransaction;

        public GenericRepository(DbContext _context)
        {
            _db = _context;
            _dbSet = _db.Set<TEntity>();
        }

        public async Task<IEnumerable<TResult>> ExecuteSqlAsync<TResult>(string sql, params object[] parameters) where TResult : class
        {
            try
            {
                return await _db.Set<TResult>().FromSqlRaw(sql, parameters).ToListAsync();
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Error executing SQL query: {e.Message}", e);
            }
        }

        public async Task<IEnumerable<TEntity>> ExecuteDbSetSqlAsync(string sql, params object[] parameters)
        {
            try
            {
                return await _dbSet.FromSqlRaw(sql, parameters).ToListAsync();
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Error executing SQL query: {e.Message}", e);
            }
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.Serializable, CancellationToken cancellationToken = default)
        {
            _currentTransaction = await _db.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
            return _currentTransaction;
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
            {
                await _db.SaveChangesAsync(cancellationToken);
                await _currentTransaction.CommitAsync(cancellationToken);
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        public virtual SetterResult Add(TEntity entity, string createdBy)
        {
            try
            {
                entity.CreateRecordStatus(createdBy);
                _dbSet.Add(entity);
                return new SetterResult() { IsException = false, Result = true, Message = CommonMessages.Success };
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = true, Result = false, Message = e.ToString(), };
            }
        }

        public virtual async Task<SetterResult> AddAsync(TEntity entity, string createdBy, CancellationToken cancellationToken)
        {
            try
            {
                entity.CreateRecordStatus(createdBy);
                await _dbSet.AddAsync(entity, cancellationToken);
                return new SetterResult() { IsException = false, Result = true, Message = CommonMessages.Success };
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = true, Result = false, Message = e.ToString() };
            }
        }

        public virtual async Task<SetterResult> AddRangeAsync(IEnumerable<TEntity> entities, string createdBy, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var entity in entities)
                {
                    entity.CreateRecordStatus(createdBy);
                }

                await _dbSet.AddRangeAsync(entities, cancellationToken);

                return new SetterResult
                {
                    IsException = false,
                    Result = true,
                    Message = CommonMessages.Success
                };
            }
            catch (Exception ex)
            {
                return new SetterResult
                {
                    IsException = true,
                    Result = false,
                    Message = ex.ToString()
                };
            }
        }

        public virtual SetterResult Delete(TEntity entity)
        {
            try
            {
                _dbSet.Remove(entity);
                return new SetterResult() { IsException = false, Result = true, Message = CommonMessages.Success };
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = true, Result = false, Message = e.ToString() };
            }
        }

        public virtual SetterResult Delete(PrimitiveType id)
        {
            try
            {
                var data = _dbSet.Find(id);
                if (data != null)
                {
                    _dbSet.Remove(data);
                    return new SetterResult() { IsException = false, Result = true, Message = CommonMessages.Success };
                }
                else
                {
                    return new SetterResult() { IsException = false, Result = false, Message = CommonMessages.NotFound };
                }
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = true, Result = false, Message = e.ToString() };
            }
        }

        public virtual async Task<SetterResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken)
        {
            try
            {
                await Task.CompletedTask;
                _dbSet.Remove(entity);
                return new SetterResult() { IsException = false, Result = true, Message = CommonMessages.Success };
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = false, Result = false, Message = e.ToString() };
            }
        }

        public virtual async Task<SetterResult> DeleteAsync(PrimitiveType id, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _dbSet.FindAsync(id, cancellationToken);
                if (data != null)
                {
                    _dbSet.Remove(data);
                    return new SetterResult() { Result = true, IsException = false, Message = CommonMessages.Success };
                }
                else
                {
                    return new SetterResult() { Result = false, IsException = false, Message = CommonMessages.NotFound };
                }
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = true, Result = false, Message = e.ToString() };
            }
        }

        public virtual async Task<SetterResult> DeleteRangeAsync(TEntity[] entities, CancellationToken cancellationToken)
        {
            try
            {
                await Task.CompletedTask;
                _dbSet.RemoveRange(entities);
                return new SetterResult() { Result = true, IsException = false, Message = CommonMessages.Success };
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = true, Result = false, Message = e.ToString() };

            }
        }

        public virtual GetterResult<IEnumerable<TEntity>> Get(Expression<Func<TEntity, bool>> filter, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy, string includeProperties = "")
        {
            try
            {
                GetterResult<IEnumerable<TEntity>> getterResult = new GetterResult<IEnumerable<TEntity>>();
                getterResult.Message = CommonMessages.Success;
                getterResult.Status = true;
                IQueryable<TEntity> query = _dbSet;

                if (filter != null)
                {
                    query = query.Where(filter);
                }

                foreach (var includeProperty in includeProperties.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProperty);
                }

                if (orderBy != null)
                {
                    getterResult.Data = orderBy(query).ToList();
                }
                else
                {
                    getterResult.Data = query.ToList();
                }
                return getterResult;
            }
            catch (Exception e)
            {

                return new GetterResult<IEnumerable<TEntity>>() { Message = e.ToString(), Status = false };
            }
        }

        public virtual async Task<GetterResult<IEnumerable<TEntity>>> GetAsync(CancellationToken cancellationToken, Expression<Func<TEntity, bool>> filter, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy, string includeProperties = "")
        {
            try
            {
                GetterResult<IEnumerable<TEntity>> getterResult = new() { Message = CommonMessages.Success, Status = true };
                IQueryable<TEntity> query = _dbSet;
                if (filter != null)
                {
                    query = query.Where(filter).Where(x => x.IsActive);
                }

                foreach (var includeProperty in includeProperties.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProperty);
                }

                if (orderBy != null)
                {
                    getterResult.Data = await orderBy(query).ToListAsync(cancellationToken);
                }
                else
                {
                    getterResult.Data = await query.ToListAsync(cancellationToken);
                }
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<IEnumerable<TEntity>>() { Message = e.ToString(), Status = false };
            }
        }

        public virtual GetterResult<bool> Any(Expression<Func<TEntity, bool>> filter)
        {
            try
            {
                GetterResult<bool> getterResult = new GetterResult<bool>();
                getterResult.Message = CommonMessages.Success;
                getterResult.Status = true;
                IQueryable<TEntity> query = _dbSet;
                getterResult.Data = query.Any(filter);
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<bool>() { Message = e.ToString(), Status = false };
            }
        }

        public virtual async Task<GetterResult<bool>> AnyAsync(Expression<Func<TEntity, bool>>? filter, CancellationToken cancellationToken)
        {
            try
            {
                var getterResult = new GetterResult<bool> { Message = CommonMessages.Success, Status = true };

                IQueryable<TEntity> query = _dbSet.Where(x => x.IsActive);

                if (filter != null)
                {
                    query = query.Where(filter);
                    getterResult.Data = await query.AnyAsync(cancellationToken);
                }
                else
                {
                    getterResult.Data = await query.AnyAsync(cancellationToken);
                }
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<bool> { Message = e.ToString(), Status = false };
            }
        }

        public virtual GetterResult<IEnumerable<TEntity>> GetAll()
        {
            try
            {
                GetterResult<IEnumerable<TEntity>> getterResult = new GetterResult<IEnumerable<TEntity>>();
                getterResult.Message = CommonMessages.Success;
                getterResult.Status = true;
                getterResult.Data = _dbSet.AsEnumerable();
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<IEnumerable<TEntity>>() { Message = e.Message, Status = false };
            }
        }

        public virtual GetterResult<TEntity> GetById(PrimitiveType id)
        {
            try
            {
                var data = _dbSet.Find(id);
                GetterResult<TEntity> getterResult = new GetterResult<TEntity>();
                getterResult.Message = CommonMessages.Success;
                getterResult.Status = true;
                getterResult.Data = data;
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<TEntity>() { Message = e.Message, Status = false };
            }
        }

        public virtual async Task<GetterResult<TEntity>> GetByIdAsync(PrimitiveType id, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _dbSet.Where(entity => entity.Id != null && entity.Id.Equals(id) && entity.IsActive).FirstOrDefaultAsync(cancellationToken);
                GetterResult<TEntity> getterResult = new()
                {
                    Message = CommonMessages.Success,
                    Status = true,
                    Data = data
                };
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<TEntity>() { Message = e.Message, Status = false, };
            }
        }

        public virtual GetterResult<IQueryable<TEntity>> GetQueryable()
        {
            try
            {
                GetterResult<IQueryable<TEntity>> getterResult = new GetterResult<IQueryable<TEntity>>();
                getterResult.Message = CommonMessages.Success;
                getterResult.Status = true;
                getterResult.Data = _dbSet.AsQueryable();
                return getterResult;
            }
            catch (Exception e)
            {

                return new GetterResult<IQueryable<TEntity>>() { Message = e.Message, Status = false };
            }
        }

        public virtual SetterResult Update(TEntity entity, string updatedBy)
        {
            try
            {
                entity.UpdateRecordStatus(updatedBy);
                _dbSet.Update(entity);
                return new SetterResult() { Message = CommonMessages.Success, Result = true, IsException = false };
            }
            catch (Exception e)
            {
                return new SetterResult() { Message = e.ToString(), Result = false, IsException = true };
            }
        }

        public SetterResult UpdateOnCondition(Expression<Func<TEntity, bool>> filter, Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setPropertyCalls, string? updatedBy = null)
        {
            try
            {
                Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> finalSet;

                if (!string.IsNullOrWhiteSpace(updatedBy))
                {
                    finalSet = set => setPropertyCalls.Compile().Invoke(set).SetProperty(x => x.UpdatedBy, _ => updatedBy).SetProperty(x => x.UpdatedDate, _ => DateTime.UtcNow);
                }
                else finalSet = setPropertyCalls;

                int rowsEffected = _dbSet.Where(filter).ExecuteUpdate(finalSet);

                return new SetterResult
                {
                    Message = $"{CommonMessages.Success}.RowsEffected:{rowsEffected}",
                    Result = true,
                    IsException = false
                };
            }
            catch (Exception e)
            {
                return new SetterResult
                {
                    Message = e.ToString(),
                    Result = false,
                    IsException = true
                };
            }
        }

        public virtual async Task<SetterResult> UpdateOnConditionAsync(Expression<Func<TEntity, bool>> filter, Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setPropertyCalls, CancellationToken cancellationToken, string? updatedBy = null)
        {
            try
            {
                IQueryable<TEntity> query = _dbSet;

                Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> finalSet;

                if (!string.IsNullOrWhiteSpace(updatedBy)) finalSet = set => setPropertyCalls.Compile().Invoke(set).SetProperty(x => x.UpdatedBy, _ => updatedBy).SetProperty(x => x.UpdatedDate, _ => DateTime.UtcNow);
                else finalSet = setPropertyCalls;

                int rowsEffected = await query.Where(filter).Where(x => x.IsActive).ExecuteUpdateAsync(finalSet, cancellationToken);

                return new SetterResult
                {
                    Message = $"{CommonMessages.Success}.RowsEffected:{rowsEffected}",
                    Result = true,
                    IsException = false,
                    Data = rowsEffected
                };
            }
            catch (Exception e)
            {
                return new SetterResult
                {
                    Message = e.ToString(),
                    Result = false,
                    IsException = true
                };
            }
        }

        public virtual SetterResult UpdateMany(TEntity[] entities, string? updatedBy = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(updatedBy)) foreach (var entity in entities) entity.UpdateRecordStatus(updatedBy);

                _dbSet.UpdateRange(entities);

                return new SetterResult
                {
                    Message = CommonMessages.Success,
                    Result = true,
                    IsException = false
                };
            }
            catch (Exception e)
            {
                return new SetterResult
                {
                    Message = e.ToString(),
                    Result = false,
                    IsException = true
                };
            }
        }

        public virtual SetterResult UpdateRange(TEntity[] entities, string? updatedBy = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(updatedBy)) foreach (var entity in entities) entity.UpdateRecordStatus(updatedBy);

                _dbSet.UpdateRange(entities);

                return new SetterResult
                {
                    Message = CommonMessages.Success,
                    Result = true,
                    IsException = false
                };
            }
            catch (Exception e)
            {
                return new SetterResult
                {
                    Message = e.ToString(),
                    Result = false,
                    IsException = true
                };
            }
        }

        public virtual SetterResult SoftDelete(TEntity entity, string updatedBy)
        {
            try
            {
                if (!entity.IsActive) return new SetterResult() { IsException = true, Result = false, Message = "Cannot update inactive entity." };
                entity.IsActive = false;
                entity.UpdateRecordStatus(updatedBy);
                _dbSet.Update(entity);
                return new SetterResult() { IsException = false, Result = true, Message = CommonMessages.Success, };
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = true, Result = false, Message = e.ToString(), };
            }
        }

        public virtual async Task<SetterResult> SoftDeleteAsync(TEntity entity, string updatedBy, CancellationToken cancellationToken)
        {
            try
            {
                if (!entity.IsActive) return new SetterResult() { IsException = true, Result = false, Message = "Cannot update inactive entity." };
                await Task.CompletedTask;
                entity.IsActive = false;
                entity.UpdateRecordStatus(updatedBy);
                _dbSet.Update(entity);
                return new SetterResult() { IsException = false, Result = true, Message = CommonMessages.Success, };
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = true, Result = false, Message = e.ToString(), };
            }
        }

        public virtual async Task<SetterResult> UpdateAsync(TEntity entity, string updatedBy, CancellationToken cancellationToken)
        {
            try
            {
                if (!entity.IsActive) return new SetterResult() { IsException = true, Result = false, Message = "Cannot update inactive entity." };
                await Task.CompletedTask;
                entity.UpdateRecordStatus(updatedBy);
                _dbSet.Update(entity);
                return new SetterResult() { IsException = false, Result = true, Message = CommonMessages.Success, };
            }
            catch (Exception e)
            {
                return new SetterResult() { IsException = true, Result = false, Message = e.ToString(), };
            }
        }

        public virtual GetterResult<TEntity> GetSingle(Expression<Func<TEntity, bool>> filter, string includeProperties = "")
        {
            try
            {
                GetterResult<TEntity> getterResult = new GetterResult<TEntity>();
                getterResult.Message = CommonMessages.Success;
                getterResult.Status = true;
                IQueryable<TEntity> query = _dbSet;

                if (filter != null)
                {
                    query = query.Where(filter);
                }

                foreach (var includeProperty in includeProperties.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProperty);
                }

                getterResult.Data = query.FirstOrDefault();
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<TEntity>() { Message = e.ToString(), Status = false };
            }
        }

        public virtual async Task<GetterResult<TEntity>> GetSingleAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken, string includeProperties = "")
        {
            try
            {
                GetterResult<TEntity> getterResult = new GetterResult<TEntity>();
                getterResult.Message = CommonMessages.Success;
                getterResult.Status = true;
                IQueryable<TEntity> query = _dbSet;
                if (filter != null)
                {
                    query = query.Where(filter).Where(x => x.IsActive);
                }
                else
                {
                    query = query.Where(x => x.IsActive);
                }
                foreach (var includeProperty in includeProperties.Split([','], StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProperty);
                }
                getterResult.Data = await query.FirstOrDefaultAsync(cancellationToken);
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<TEntity>() { Message = e.ToString(), Status = false };
            }
        }

        public virtual async Task<GetterResult<IEnumerable<TEntity>>> GetAllAsync(CancellationToken cancellationToken)
        {
            try
            {
                GetterResult<IEnumerable<TEntity>> getterResult = new()
                {
                    Message = CommonMessages.Success,
                    Status = true,
                    Data = await _dbSet.Where(x => x.IsActive).ToListAsync(cancellationToken)
                };
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<IEnumerable<TEntity>>() { Message = e.Message, Status = false };
            }
        }

        public virtual GetterResult<bool> All(Expression<Func<TEntity, bool>> filter)
        {

            try
            {
                GetterResult<bool> getterResult = new() { Message = CommonMessages.Success, Status = true };
                IQueryable<TEntity> query = _dbSet;
                getterResult.Data = query.All(filter);
                return getterResult;
            }
            catch (Exception e)
            {
                return new GetterResult<bool>() { Message = e.ToString(), Status = false };
            }
        }

    }
}