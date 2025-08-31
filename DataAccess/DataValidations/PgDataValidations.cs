using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Omni_MVC_2.DataAccess.UnitOfWork;
using Omni_MVC_2.Utilities.RepositoryUtilities;

namespace Omni_MVC_2.DataAccess.DataValidations
{
    public class PgDataValidations : IDataValidations
    {
        private readonly IUnitOfWork _uow;
        private static readonly ConcurrentDictionary<string, Type?> _entityTypeCache = new();

        public PgDataValidations(IUnitOfWork uow)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        #region IsUnique Summary
        /// <summary>
        /// Generates an asynchronous validation function that checks whether a given string property value is unique across all instances
        /// of a dynamically resolved entity type. This is particularly useful when the entity type is only known at runtime, such as when
        /// using metadata-driven forms or admin panels.
        /// </summary>
        /// <typeparam name="TDto">The type of the DTO being validated.</typeparam>
        /// <param name="entityTypeName">The name of the entity type (e.g., "Company", "Plant_Location"). Must match the class name.</param>
        /// <param name="entityPropertyName">The name of the string property on the entity to check for uniqueness (e.g., "Name").</param>
        /// <param name="dtoIdProvider">
        /// Optional. A function to retrieve the ID from the DTO, used to exclude the current record when performing uniqueness checks during updates.
        /// </param>
        /// <param name="sourceFactory">
        /// Optional. A function that returns an <see cref="IQueryable"/> of the target entity type from the <see cref="IUnitOfWork"/>.
        /// If not provided, the method uses reflection to locate the appropriate repository and extract its data.
        /// </param>
        /// <param name="entityIdPropertyName">
        /// Optional. The name of the entity's ID property (used for exclusion when updating). Defaults to "Id".
        /// </param>
        /// <returns>
        /// A <see cref="Func{TDto, String, CancellationToken, Task{bool}}"/> that returns <c>true</c> if the given value is unique
        /// for the specified entity and property; otherwise, <c>false</c>.
        /// </returns>
        #endregion IsUnique Summary
        public Func<TDto, string?, CancellationToken, Task<bool>> IsUnique<TDto>(string entityTypeName, string entityPropertyName, Func<TDto, object?>? dtoIdProvider = null, Func<IUnitOfWork, IQueryable>? sourceFactory = null, string? entityIdPropertyName = null)
        {
            if (string.IsNullOrWhiteSpace(entityTypeName)) throw new ArgumentNullException(nameof(entityTypeName));
            if (string.IsNullOrWhiteSpace(entityPropertyName)) throw new ArgumentNullException(nameof(entityPropertyName));

            return async (dto, value, ct) =>
            {
                if (string.IsNullOrWhiteSpace(value)) return true; // let NotEmpty handle empties

                // 1) resolve entity Type by name (cached)
                var entityType = _entityTypeCache.GetOrAdd(entityTypeName, name =>
                {
                    // search loaded assemblies for a type whose Name matches the provided entityTypeName
                    var found = AppDomain.CurrentDomain.GetAssemblies()
                                .SelectMany(a =>
                                {
                                    try { return a.GetTypes(); }
                                    catch { return Array.Empty<Type>(); } // skip dynamic / reflection-only that throw
                                })
                                .FirstOrDefault(t => t.Name == name || t.FullName?.EndsWith("." + name) == true);
                    return found;
                }) ?? throw new InvalidOperationException($"Could not find entity type '{entityTypeName}' in loaded assemblies. Ensure domain model assembly is loaded.");

                // 2) obtain IQueryable (either via explicit factory or reflection on _uow repos as before)
                IQueryable? source = null;
                if (sourceFactory != null)
                {
                    source = sourceFactory(_uow) ?? throw new InvalidOperationException("sourceFactory returned null.");
                }
                else
                {
                    // Reflection: find IGenericRepository<TEntity, TKey> property on the UoW that has TEntity == entityType
                    var repoProp = _uow.GetType()
                                       .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                       .FirstOrDefault(p =>
                                       {
                                           if (!p.PropertyType.IsGenericType) return false;
                                           var def = p.PropertyType.GetGenericTypeDefinition();
                                           if (def != typeof(IRepository<,>)) return false;
                                           var args = p.PropertyType.GetGenericArguments();
                                           return args[0] == entityType;
                                       }) ?? throw new InvalidOperationException($"Could not find IGenericRepository<{entityType.Name}, TKey> on {_uow.GetType().Name}. Pass explicit sourceFactory if needed.");

                    var repo = repoProp.GetValue(_uow) ?? throw new InvalidOperationException($"Repository property '{repoProp.Name}' returned null.");

                    // call repo.GetQueryable() and read .Data (same assumptions as your current code)
                    var getQueryableMethod = repo.GetType().GetMethod("GetQueryable", Type.EmptyTypes) ?? throw new InvalidOperationException($"Repository '{repoProp.Name}' does not expose GetQueryable().");

                    var queryableWrapper = getQueryableMethod.Invoke(repo, null) ?? throw new InvalidOperationException($"GetQueryable() returned null for repository '{repoProp.Name}'.");

                    var dataProp = queryableWrapper.GetType().GetProperty("Data", BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException($"GetQueryable().Result does not expose 'Data' for repository '{repoProp.Name}'.");

                    source = dataProp.GetValue(queryableWrapper) as IQueryable ?? throw new InvalidOperationException($"Unable to obtain IQueryable from {repoProp.Name}.GetQueryable().Data");
                }

                // 3) Build expression for entity property: e => e.{entityPropertyName} != null && e.{prop}.ToLower() == value.ToLower() [ plus optional filters and exclusion by Id ]
                var param = Expression.Parameter(entityType, "e");
                var propInfo = entityType.GetProperty(entityPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                               ?? throw new InvalidOperationException($"Entity '{entityType.Name}' does not contain a public property named '{entityPropertyName}'.");
                if (propInfo.PropertyType != typeof(string))
                    throw new InvalidOperationException($"Property '{entityPropertyName}' on '{entityType.Name}' is not a string; this helper expects string properties.");

                var propAccess = Expression.Property(param, propInfo);
                var notNullExpr = Expression.NotEqual(propAccess, Expression.Constant(null, typeof(string)));

                var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
                var leftToLower = Expression.Call(propAccess, toLowerMethod);
                var rightConst = Expression.Constant(value.ToLower());
                var equalExpr = Expression.Equal(leftToLower, rightConst);

                Expression predicate = Expression.AndAlso(notNullExpr, equalExpr);

                // optional IsActive && !IsArchived if present
                var isActiveProp = entityType.GetProperty("IsActive");
                if (isActiveProp != null && isActiveProp.PropertyType == typeof(bool))
                {
                    predicate = Expression.AndAlso(predicate, Expression.Property(param, isActiveProp));
                }

                var isArchivedProp = entityType.GetProperty("IsArchived");
                if (isArchivedProp != null && isArchivedProp.PropertyType == typeof(bool))
                {
                    predicate = Expression.AndAlso(predicate, Expression.Not(Expression.Property(param, isArchivedProp)));
                }

                // exclude same entity when updating (dtoIdProvider supplied)
                if (dtoIdProvider != null && !string.IsNullOrEmpty(entityIdPropertyName))
                {
                    var hasIdProp = entityType.GetProperty(entityIdPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (hasIdProp != null)
                    {
                        var dtoId = dtoIdProvider(dto);
                        if (dtoId != null)
                        {
                            var leftId = Expression.Property(param, hasIdProp);
                            var rightId = Expression.Constant(Convert.ChangeType(dtoId, hasIdProp.PropertyType), hasIdProp.PropertyType);
                            predicate = Expression.AndAlso(predicate, Expression.NotEqual(leftId, rightId));
                        }
                    }
                }

                // 4) Build lambda with runtime type and call EF AnyAsync<T> via reflection build delegate type Func<TEntity,bool>
                var delegateType = typeof(Func<,>).MakeGenericType(entityType, typeof(bool));

                // Create LambdaExpression with the delegate type (Expression.Lambda will return a LambdaExpression whose runtime Type == Expression<Func<TEntity,bool>>)
                var lambda = Expression.Lambda(delegateType, predicate, param);

                // Find EF Core AnyAsync<TSource>(IQueryable<TSource>, Expression<Func<TSource, bool>>, CancellationToken)
                var anyAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m =>
                    {
                        if (m.Name != nameof(EntityFrameworkQueryableExtensions.AnyAsync)) return false;
                        var parameters = m.GetParameters();
                        if (parameters.Length < 2) return false;
                        // ensure second parameter is an Expression<>
                        return parameters[1].ParameterType.IsGenericType && parameters[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>);
                    })
                    ?? throw new InvalidOperationException("Could not find EntityFrameworkQueryableExtensions.AnyAsync method via reflection.");

                // Make generic for TEntity
                var anyAsyncGeneric = anyAsyncMethod.MakeGenericMethod(entityType);

                // Invoke AnyAsync<TEntity>(source, (Expression<Func<TEntity,bool>>)lambda, ct)
                var taskObj = anyAsyncGeneric.Invoke(null, [source, lambda, ct])
                              ?? throw new InvalidOperationException("AnyAsync invocation returned null Task.");

                // Await Task<bool> result
                var exists = await ((Task<bool>)taskObj).ConfigureAwait(false);
                return !exists;
            };
        }

        #region IsUniqueOnUpdate Summary
        /// <summary>
        /// Generates an asynchronous validation function that checks whether a string property value on a DTO is unique 
        /// within the corresponding entity set, excluding the current entity (based on its ID). 
        /// This is typically used for update operations to ensure uniqueness constraints are maintained.
        /// </summary>
        /// <typeparam name="TDto">The type of the DTO being validated. It must contain an "Id" property and the property to validate.</typeparam>
        /// <param name="entityTypeName">
        /// The name of the target entity class (e.g., "Company", "Plant_Location"). Must match the class name defined in the domain model.
        /// </param>
        /// <param name="entityPropertyName">
        /// The name of the string property on the entity to validate for uniqueness (e.g., "Name", "Code").
        /// This property must also exist on the DTO being validated.
        /// </param>
        /// <param name="dtoIdPropertyName">
        /// Optional. The name of the ID property on the DTO. Defaults to "Id".
        /// Used to match the entity's ID against the corresponding DTO.
        /// </param>
        /// <returns>
        /// A <see cref="Func{TDto, CancellationToken, Task{bool}}"/> that returns <c>true</c> if the value is unique 
        /// (excluding the entity with the same ID), otherwise <c>false</c>.
        /// </returns>
        #endregion IsUniqueOnUpdate Summary
        public Func<TDto, CancellationToken, Task<bool>> IsUniqueOnUpdate<TDto>(string entityTypeName, string entityPropertyName, string dtoIdPropertyName = "Id")
        {
            if (string.IsNullOrWhiteSpace(entityTypeName)) throw new ArgumentNullException(nameof(entityTypeName));
            if (string.IsNullOrWhiteSpace(entityPropertyName)) throw new ArgumentNullException(nameof(entityPropertyName));

            return async (dto, ct) =>
            {
                // Read value and Id from DTO
                var dtoType = typeof(TDto);
                var dtoValueProp = dtoType.GetProperty(entityPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                var dtoIdProp = dtoType.GetProperty(dtoIdPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (dtoValueProp == null) throw new InvalidOperationException($"DTO '{dtoType.Name}' does not contain '{entityPropertyName}'.");
                var value = dtoValueProp.GetValue(dto) as string;
                if (string.IsNullOrWhiteSpace(value)) return true;
                if (dtoIdProp == null) throw new InvalidOperationException($"DTO '{dtoType.Name}' does not contain an {dtoIdPropertyName} property.");
                var dtoId = dtoIdProp.GetValue(dto);

                // 1) Resolve entityType by name
                var entityType = _entityTypeCache.GetOrAdd(entityTypeName, name =>
                {
                    return AppDomain.CurrentDomain.GetAssemblies()
                      .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                      .FirstOrDefault(t => t.Name == name || t.FullName?.EndsWith("." + name) == true);
                }) ?? throw new InvalidOperationException($"Entity type '{entityTypeName}' not found.");

                // 2) Find the corresponding repository
                var repoProp = _uow.GetType()
                    .GetProperties()
                    .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                                         p.PropertyType.GetGenericTypeDefinition() == typeof(IRepository<,>) &&
                                         p.PropertyType.GetGenericArguments()[0] == entityType
                    ) ?? throw new InvalidOperationException($"Repository for {entityType.Name} not found on UnitOfWork.");

                var repo = repoProp.GetValue(_uow)!;
                var getQueryableMethod = repo.GetType().GetMethod("GetQueryable")!;
                var queryableWrapper = getQueryableMethod.Invoke(repo, null)!;
                var dataProp = queryableWrapper.GetType().GetProperty("Data")!;
                var source = (IQueryable)dataProp.GetValue(queryableWrapper)!;

                // 3) Build predicate: match name AND entity.Id != dto.Id
                var param = Expression.Parameter(entityType, "e");

                var entityValueProp = entityType.GetProperty(entityPropertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)!;
                var propAccess = Expression.Property(param, entityValueProp);
                var notNullExpr = Expression.NotEqual(propAccess, Expression.Constant(null, typeof(string)));
                var toLower = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
                var left = Expression.Call(propAccess, toLower);
                var right = Expression.Constant(value.ToLower());
                var nameEqual = Expression.Equal(left, right);

                Expression predicate = Expression.AndAlso(notNullExpr, nameEqual);

                // Exclude current row: e.Id != dto.Id
                var idProp = entityType.GetProperty("Id", BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (idProp != null && dtoId != null)
                {
                    var leftId = Expression.Property(param, idProp);
                    var rightId = Expression.Constant(Convert.ChangeType(dtoId, idProp.PropertyType), idProp.PropertyType);
                    predicate = Expression.AndAlso(predicate, Expression.NotEqual(leftId, rightId));
                }

                var funcType = typeof(Func<,>).MakeGenericType(entityType, typeof(bool));
                var lambda = Expression.Lambda(funcType, predicate, param);

                var anyAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                                    .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AnyAsync))
                                    .Where(m => m.IsGenericMethod)
                                    .Select(m => new { Method = m, Params = m.GetParameters() })
                                    .FirstOrDefault(x =>
                                        x.Params.Length == 3 &&
                                        x.Params[0].ParameterType.IsGenericType &&
                                        x.Params[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>) &&
                                        x.Params[1].ParameterType.IsGenericType &&
                                        x.Params[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) &&
                                        x.Params[2].ParameterType == typeof(CancellationToken)
                                    )?.Method.MakeGenericMethod(entityType) ?? throw new InvalidOperationException("Could not find the expected AnyAsync<TEntity> method.");

                var task = (Task<bool>)anyAsyncMethod.Invoke(null, [source, lambda, ct])!;
                var exists = await task.ConfigureAwait(false);
                return !exists;
            };
        }

        #region IdExists Summary
        /// <summary>
        /// Generates an asynchronous validation function that checks whether an entity with the same ID as the provided DTO exists 
        /// in the database. This is useful for verifying the existence of an entity before performing update or delete operations,
        /// especially in distributed or decoupled systems where stale data might be passed in.
        /// </summary>
        /// <typeparam name="TDto">The type of the DTO being validated. It must contain an "Id" property.</typeparam>
        /// <param name="entityTypeName">
        /// The name of the entity class (e.g., "Company", "Plant", "Document_Numbering") as defined in your domain model.
        /// This is used to resolve the entity type at runtime via reflection.
        /// </param>
        /// <param name="entityIdPropertyName">
        /// Optional. The name of the ID property on the entity. Defaults to "Id".
        /// Used to match the DTO's ID against the corresponding entity.
        /// </param>
        /// /// <param name="dtoIdPropertyName">
        /// Optional. The name of the ID property on the DTO. Defaults to "Id".
        /// Used to match the entity's ID against the corresponding DTO.
        /// </param>
        /// <returns>
        /// A <see cref="Func{TDto, CancellationToken, Task{bool}}"/> delegate that returns <c>true</c> if an entity with the same ID exists; 
        /// otherwise, <c>false</c>.
        /// </returns>
        #endregion IdExists Summary
        public Func<TDto, CancellationToken, Task<bool>> IdExists<TDto>(string entityTypeName, string entityIdPropertyName = "Id", string dtoIdPropertyName = "Id")
        {
            if (string.IsNullOrWhiteSpace(entityTypeName)) throw new ArgumentNullException(nameof(entityTypeName));

            return async (dto, ct) =>
            {
                // Pull Id off DTO
                var dtoType = typeof(TDto);
                var dtoIdProp = dtoType.GetProperty(dtoIdPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException($"DTO '{dtoType.Name}' does not contain an {dtoIdPropertyName} property.");
                var dtoIdObj = dtoIdProp.GetValue(dto);
                if (dtoIdObj == null) return false;

                // Resolve entity type
                var entityType = _entityTypeCache.GetOrAdd(entityTypeName, name =>
                {
                    return AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                        .FirstOrDefault(t => t.Name == name || t.FullName?.EndsWith("." + name) == true);
                }) ?? throw new InvalidOperationException($"Could not find entity type '{entityTypeName}' in loaded assemblies.");

                // Grab IQueryable via UnitOfWork
                var repoProp = _uow.GetType()
                    .GetProperties()
                    .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                                         p.PropertyType.GetGenericTypeDefinition() == typeof(IRepository<,>) &&
                                         p.PropertyType.GetGenericArguments()[0] == entityType
                    )
                    ?? throw new InvalidOperationException($"Could not find IGenericRepository<{entityType.Name}, TKey> on UnitOfWork.");

                var repo = repoProp.GetValue(_uow)!;
                var getQueryable = repo.GetType().GetMethod("GetQueryable", Type.EmptyTypes) ?? throw new InvalidOperationException($"Repository '{repoProp.Name}' does not expose GetQueryable().");

                var wrapper = getQueryable.Invoke(repo, null)!;
                var dataProp = wrapper.GetType().GetProperty("Data")! ?? throw new InvalidOperationException($"GetQueryable result does not contain Data.");
                var source = (IQueryable)dataProp.GetValue(wrapper)!;

                // e => e.Id == dtoId [&& IsActive && !IsArchived]
                var param = Expression.Parameter(entityType, "e");

                var idProp = entityType.GetProperty(entityIdPropertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException($"Entity '{entityType.Name}' does not contain '{entityIdPropertyName}'.");
                var left = Expression.Property(param, idProp);
                var right = Expression.Constant(Convert.ChangeType(dtoIdObj, idProp.PropertyType), idProp.PropertyType);
                Expression predicate = Expression.Equal(left, right);

                // Optional active flags
                var isActiveProp = entityType.GetProperty("IsActive");
                if (isActiveProp != null && isActiveProp.PropertyType == typeof(bool)) predicate = Expression.AndAlso(predicate, Expression.Property(param, isActiveProp));
                var isArchivedProp = entityType.GetProperty("IsArchived");
                if (isArchivedProp != null && isArchivedProp.PropertyType == typeof(bool)) predicate = Expression.AndAlso(predicate, Expression.Not(Expression.Property(param, isArchivedProp)));

                var funcType = typeof(Func<,>).MakeGenericType(entityType, typeof(bool));
                var lambda = Expression.Lambda(funcType, predicate, param);

                var anyAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                                    .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AnyAsync))
                                    .Where(m => m.IsGenericMethod)
                                    .Select(m => new { Method = m, Params = m.GetParameters() })
                                    .FirstOrDefault(x =>
                                        x.Params.Length == 3 &&
                                        x.Params[0].ParameterType.IsGenericType &&
                                        x.Params[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>) &&
                                        x.Params[1].ParameterType.IsGenericType &&
                                        x.Params[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) &&
                                        x.Params[2].ParameterType == typeof(CancellationToken)
                                    )?.Method.MakeGenericMethod(entityType) ?? throw new InvalidOperationException("Could not find the expected AnyAsync<TEntity> method.");

                var task = (Task<bool>)anyAsyncMethod.Invoke(null, [source, lambda, ct])!;
                return await task.ConfigureAwait(false);
            };
        }

        #region IsFkExists Summary
        /// <summary>
        /// Creates an asynchronous validation function that checks whether a foreign key value from a DTO exists
        /// as a corresponding primary key in a specified entity in the database. This method ensures referential integrity
        /// when referencing related entities.
        /// </summary>
        /// <typeparam name="TDto">
        /// The type of the DTO being validated. It must contain a public property matching <paramref name="foreignDtoIdPropertyName"/>.
        /// </typeparam>
        /// <param name="foreignEntityName">
        /// The name of the entity type to validate against (e.g., "Plant", "Company"). This type must exist in the application's loaded assemblies.
        /// </param>
        /// <param name="foreignEntityIdPropertyName">
        /// The name of the scalar (key) property on the target foreign entity to be matched (e.g., "Id", "PlantId").
        /// This can also be a dotted path to nested properties (e.g., "Plant.Id").
        /// </param>
        /// <param name="foreignDtoIdPropertyName">
        /// The name of the property on the DTO that holds the foreign key value to be validated.
        /// </param>
        /// <returns>
        /// A <see cref="Func{TDto, CancellationToken, Task{bool}}"/> delegate that, when executed, returns <c>true</c>
        /// if the foreign key value exists in the specified entity; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method dynamically resolves types and repositories via reflection and builds a LINQ expression to perform the existence check.
        /// It also supports optional filtering on <c>IsActive == true</c> and <c>IsArchived == false</c> if those properties exist on the entity.
        /// Throws detailed exceptions if types, properties, or repositories are not found or if types are mismatched.
        /// </remarks>
        #endregion IsFkExists Summary
        public Func<TDto, CancellationToken, Task<bool>> IsFkExists<TDto>(string foreignEntityName, string foreignDtoIdPropertyName, string foreignEntityIdPropertyName = "Id")
        {
            if (string.IsNullOrWhiteSpace(foreignEntityName)) throw new ArgumentNullException(nameof(foreignEntityName));
            if (string.IsNullOrWhiteSpace(foreignEntityIdPropertyName)) throw new ArgumentNullException(nameof(foreignEntityIdPropertyName));
            if (string.IsNullOrWhiteSpace(foreignDtoIdPropertyName)) throw new ArgumentNullException(nameof(foreignDtoIdPropertyName));

            object? ConvertToType(object? value, Type targetType)
            {
                if (value == null) return null;
                var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (nonNullable.IsInstanceOfType(value)) return value;
                try
                {
                    if (nonNullable == typeof(Guid))
                    {
                        if (value is Guid g) return g;
                        if (value is string s) return Guid.Parse(s);
                        return new Guid(Convert.ToString(value)!);
                    }
                    if (nonNullable.IsEnum)
                    {
                        if (value is string s) return Enum.Parse(nonNullable, s, ignoreCase: true);
                        return Enum.ToObject(nonNullable, Convert.ChangeType(value, Enum.GetUnderlyingType(nonNullable)));
                    }
                    return Convert.ChangeType(value, nonNullable);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to convert DTO value '{value}' ({value?.GetType().Name}) to target CLR type {targetType.FullName}: {ex.Message}", ex);
                }
            }

            bool IsSimpleClrType(Type t)
            {
                var nonNullable = Nullable.GetUnderlyingType(t) ?? t;
                if (nonNullable.IsPrimitive) return true;
                if (nonNullable.IsEnum) return true;
                if (nonNullable == typeof(string)) return true;
                if (nonNullable == typeof(decimal)) return true;
                if (nonNullable == typeof(Guid)) return true;
                if (nonNullable == typeof(DateTime)) return true;
                if (nonNullable == typeof(DateTimeOffset)) return true;
                if (nonNullable == typeof(TimeSpan)) return true;
                return false;
            }

            Expression BuildPropertyAccess(Expression param, Type type, string propertyPath)
            {
                var parts = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                Expression current = param;
                var curType = type;
                foreach (var p in parts)
                {
                    var prop = curType.GetProperty(p, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                ?? throw new InvalidOperationException($"Property '{p}' not found on '{curType.Name}' while walking path '{propertyPath}'.");
                    current = Expression.Property(current, prop);
                    curType = prop.PropertyType;
                }
                return current;
            }

            return async (dto, ct) =>
            {
                // 1) read dto foreign id
                var dtoType = typeof(TDto);
                var dtoForeignProp = dtoType.GetProperty(foreignDtoIdPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException($"DTO '{dtoType.Name}' does not contain property '{foreignDtoIdPropertyName}'.");
                var dtoForeignVal = dtoForeignProp.GetValue(dto);
                if (dtoForeignVal == null) return false; // NotEmpty should be used in rule for friendlier message

                // 2) resolve entity CLR type
                var entityType = _entityTypeCache.GetOrAdd(foreignEntityName, name =>
                {
                    return AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                        .FirstOrDefault(t => t.Name == name || t.FullName?.EndsWith("." + name) == true);
                }) ?? throw new InvalidOperationException($"Entity type '{foreignEntityName}' not found in loaded assemblies.");

                // 3) get IQueryable source
                var repoProp = _uow.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p =>
                    {
                        if (!p.PropertyType.IsGenericType) return false;
                        var def = p.PropertyType.GetGenericTypeDefinition();
                        if (def != typeof(IRepository<,>)) return false;
                        var args = p.PropertyType.GetGenericArguments();
                        return args[0] == entityType;
                    }) ?? throw new InvalidOperationException($"Repository for '{entityType.Name}' not found on UnitOfWork.");

                var repo = repoProp.GetValue(_uow) ?? throw new InvalidOperationException($"Repository '{repoProp.Name}' returned null.");
                var getQueryableMethod = repo.GetType().GetMethod("GetQueryable", Type.EmptyTypes) ?? throw new InvalidOperationException($"Repository '{repoProp.Name}' does not expose GetQueryable().");
                var wrapper = getQueryableMethod.Invoke(repo, null) ?? throw new InvalidOperationException($"GetQueryable() returned null for '{repoProp.Name}'.");
                var dataProp = wrapper.GetType().GetProperty("Data", BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException($"GetQueryable().Result does not expose 'Data' for repository '{repoProp.Name}'.");
                var source = dataProp.GetValue(wrapper) as IQueryable ?? throw new InvalidOperationException($"Unable to obtain IQueryable from {repoProp.Name}.GetQueryable().Data");

                // 4) left expression
                var param = Expression.Parameter(entityType, "e");
                var leftExpr = BuildPropertyAccess(param, entityType, foreignEntityIdPropertyName);

                var leftType = leftExpr.Type;
                // fail early if left side is not a simple CLR type (prevents trying to convert string -> complex nav)
                if (!IsSimpleClrType(leftType)) throw new InvalidOperationException($"Property '{foreignEntityIdPropertyName}' on '{entityType.Name}' is of type '{leftType.FullName}', which is not a scalar/key type. Provide a scalar property (e.g. 'Id' or 'PlantLocationId').");

                // 5) convert DTO value
                var convertedValue = ConvertToType(dtoForeignVal, leftType);
                var rightConst = Expression.Constant(convertedValue, leftType);
                Expression predicate = Expression.Equal(leftExpr, rightConst);

                // Optional Flags: IsActive && !IsArchived
                var isActiveProp = entityType.GetProperty("IsActive", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (isActiveProp != null && isActiveProp.PropertyType == typeof(bool)) predicate = Expression.AndAlso(predicate, Expression.Property(param, isActiveProp));
                var isArchivedProp = entityType.GetProperty("IsArchived", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (isArchivedProp != null && isArchivedProp.PropertyType == typeof(bool)) predicate = Expression.AndAlso(predicate, Expression.Not(Expression.Property(param, isArchivedProp)));
                var funcType = typeof(Func<,>).MakeGenericType(entityType, typeof(bool));
                var lambda = Expression.Lambda(funcType, predicate, param);

                // 6) call EF AnyAsync via reflection
                var anyAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AnyAsync) && m.IsGenericMethod)
                    .Select(m => new { Method = m, Params = m.GetParameters() })
                    .FirstOrDefault(x =>
                        x.Params.Length == 3 &&
                        x.Params[0].ParameterType.IsGenericType &&
                        x.Params[0].ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>) &&
                        x.Params[1].ParameterType.IsGenericType &&
                        x.Params[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) &&
                        x.Params[2].ParameterType == typeof(CancellationToken)
                    )?.Method.MakeGenericMethod(entityType)
                    ?? throw new InvalidOperationException("Could not find EntityFrameworkQueryableExtensions.AnyAsync via reflection.");

                var task = (Task<bool>)anyAsyncMethod.Invoke(null, new object[] { source, lambda, ct })!;
                return await task.ConfigureAwait(false);
            };
        }
    }
}