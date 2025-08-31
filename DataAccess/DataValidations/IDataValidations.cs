using Omni_MVC_2.DataAccess.UnitOfWork;

namespace Omni_MVC_2.DataAccess.DataValidations
{
    public interface IDataValidations
    {
        Func<TDto, string?, CancellationToken, Task<bool>> IsUnique<TDto>(string entityTypeName, string entityPropertyName, Func<TDto, object?>? dtoIdProvider = null, Func<IUnitOfWork, IQueryable>? sourceFactory = null, string entityIdPropertyName = "Id");
        Func<TDto, CancellationToken, Task<bool>> IsUniqueOnUpdate<TDto>(string entityTypeName, string entityPropertyName, string dtoIdPropertyName = "Id");
        Func<TDto, CancellationToken, Task<bool>> IdExists<TDto>(string entityTypeName, string entityIdPropertyName = "Id", string dtoIdPropertyName = "Id");
        Func<TDto, CancellationToken, Task<bool>> IsFkExists<TDto>(string foreignEntityName, string foreignDtoIdPropertyName, string foreignEntityIdPropertyName = "Id");
    }
}