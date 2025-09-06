using System.Dynamic;
using System.Linq.Expressions;

namespace Omni_MVC_2.Utilities.RepositoryUtilities
{
    public record CustomQueryableExtensions<T>(IQueryable<T> Source)
    {
        #region Filter
        /// <summary>
        /// Filters the source by applying a null-safe equality check on a nested property path.
        /// 
        /// This method wraps the <see cref="BuildNullSafeEquals"/> helper to safely compare
        /// a possibly nested property against a given value, guarding against null references
        /// on intermediate properties. It returns a new <see cref="CustomQueryableExtensions{T}"/> with
        /// the filtered query applied.
        /// 
        /// Usage example:
        /// <code>
        /// queryEngine.WhereNullSafeEquals("Address.City.Name", "Seattle");
        /// </code>
        /// 
        /// This will safely filter elements where Address and City are not null and
        /// City.Name equals "Seattle".
        /// </summary>
        /// <param name="path">Dot-separated nested property path</param>
        /// <param name="value">Value to compare for equality</param>
        /// <returns>A new QueryEngine with the null-safe equality filter applied</returns>
        public CustomQueryableExtensions<T> WhereNullSafeEquals(string path, object value) => new(Source.Where(BuildNullSafeEquals(path, value)));

        /// <summary>
        /// Filters items where the property at <paramref name="propertyPath"/> equals the specified <paramref name="value"/>.
        /// </summary>
        public CustomQueryableExtensions<T> WhereEquals(string propertyPath, object value)
        {
            var predicate = BuildComparisonExpression(propertyPath, ExpressionType.Equal, value);
            return new CustomQueryableExtensions<T>(Source.Where(predicate));
        }

        /// <summary>
        /// Filters items where the property at <paramref name="propertyPath"/> is greater than the specified <paramref name="value"/>.
        /// </summary>
        public CustomQueryableExtensions<T> WhereGreaterThan(string propertyPath, object value)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var prop = BuildNestedPropertyExpression(parameter, propertyPath);
            if (!IsComparableType(prop.Type)) throw new InvalidOperationException($"Property '{propertyPath}' is of type '{prop.Type}' which is not comparable.");
            var predicate = BuildComparisonExpression(propertyPath, ExpressionType.GreaterThan, value);
            return new CustomQueryableExtensions<T>(Source.Where(predicate));
        }

        /// <summary>
        /// Filters items where the property at <paramref name="propertyPath"/> is less than the specified <paramref name="value"/>.
        /// </summary>
        public CustomQueryableExtensions<T> WhereLessThan(string propertyPath, object value)
        {
            var predicate = BuildComparisonExpression(propertyPath, ExpressionType.LessThan, value);
            return new CustomQueryableExtensions<T>(Source.Where(predicate));
        }

        /// <summary>
        /// Filters DateTime properties where the <c>Date</c> (ignoring time) matches the provided date.
        /// Useful for filtering records by day regardless of time of day.
        /// </summary>
        /// <param name="propertyPath">The dot-separated property path of the DateTime field.</param>
        /// <param name="date">The date to compare against (time is ignored).</param>
        /// <returns>A filtered <see cref="CustomQueryableExtensions{T}"/> instance.</returns>
        public CustomQueryableExtensions<T> WhereDateEquals(string propertyPath, DateTime date)
        {
            var predicate = BuildDateEquals(propertyPath, date);
            return new CustomQueryableExtensions<T>(Source.Where(predicate));
        }

        /// <summary>
        /// Filters string properties that contain the specified substring, case-insensitively.
        /// Supports nested properties (e.g., "User.Email").
        /// </summary>
        /// <param name="propertyPath">The dot-separated property path of the string field.</param>
        /// <param name="term">The case-insensitive substring to search for.</param>
        /// <returns>A filtered <see cref="CustomQueryableExtensions{T}"/> instance.</returns>
        public CustomQueryableExtensions<T> WhereContainsCaseInsensitive(string propertyPath, string term)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var prop = BuildNestedPropertyExpression(parameter, propertyPath);
            if (prop.Type != typeof(string)) throw new InvalidOperationException($"Property '{propertyPath}' must be of type string for 'Contains'.");
            var predicate = BuildCaseInsensitiveContains(propertyPath, term);
            return new CustomQueryableExtensions<T>(Source.Where(predicate));
        }

        /// <summary>
        /// Filters string properties containing the specified substring.
        /// </summary>
        public CustomQueryableExtensions<T> WhereContains(string propertyPath, string substring)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var prop = BuildNestedPropertyExpression(parameter, propertyPath);
            if (prop.Type != typeof(string)) throw new InvalidOperationException($"Property '{propertyPath}' must be of type string for 'Contains'.");
            var predicate = BuildStringMethodCall(propertyPath, "Contains", substring);
            return new CustomQueryableExtensions<T>(Source.Where(predicate));
        }

        /// <summary>
        /// Filters collections Where collection property at 'collectionPath' has Any(item => item == value)
        /// </summary>
        public CustomQueryableExtensions<T> WhereCollectionAny<TItem>(string collectionPath, object value)
        {
            var param = Expression.Parameter(typeof(T), "x");
            var collection = BuildNestedPropertyExpression(param, collectionPath); // should be IEnumerable<TItem>
            var itemParam = Expression.Parameter(typeof(TItem), "i");
            var equals = Expression.Equal(itemParam, Expression.Constant(Convert.ChangeType(value, typeof(TItem))));
            var lambda = Expression.Lambda<Func<TItem, bool>>(equals, itemParam);
            var anyCall = Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), new[] { typeof(TItem) }, collection, lambda);
            return new CustomQueryableExtensions<T>(Source.Where(Expression.Lambda<Func<T, bool>>(anyCall, param)));
        }

        /// <summary>
        /// Filters string properties starting with the specified prefix.
        /// </summary>
        public CustomQueryableExtensions<T> WhereStartsWith(string propertyPath, string prefix)
        {
            var predicate = BuildStringMethodCall(propertyPath, "StartsWith", prefix);
            return new CustomQueryableExtensions<T>(Source.Where(predicate));
        }

        /// <summary>
        /// Filters string properties ending with the specified suffix.
        /// </summary>
        public CustomQueryableExtensions<T> WhereEndsWith(string propertyPath, string suffix)
        {
            var predicate = BuildStringMethodCall(propertyPath, "EndsWith", suffix);
            return new CustomQueryableExtensions<T>(Source.Where(predicate));
        }

        /// <summary>
        /// Filters the query to include only those items where the property value is contained in the given set of values.
        /// Implements an expression equivalent to: x => values.Contains(x.Prop)
        /// </summary>
        /// <param name="propertyPath">The dotted path to the property (supports nested properties).</param>
        /// <param name="values">The collection of values to match against.</param>
        /// <returns>A new QueryEngine with the filtered IQueryable.</returns>
        public CustomQueryableExtensions<T> WhereIn(string propertyPath, IEnumerable<object> values)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var prop = BuildNestedPropertyExpression(parameter, propertyPath);
            var converted = Expression.Convert(prop, typeof(object));
            var contains = Expression.Call(
                Expression.Constant(values),
                "Contains",                  // Use string method name, not nameof()
                Type.EmptyTypes,
                converted
            );
            var lambda = Expression.Lambda<Func<T, bool>>(contains, parameter);
            return new CustomQueryableExtensions<T>(Source.Where(lambda));
        }

        /// <summary>
        /// Filters the query to include only those items where the property value falls between min and max (inclusive).
        /// Implements an expression equivalent to: x => x.Prop >= min && x.Prop <= max
        /// </summary>
        /// <param name="propertyPath">The dotted path to the property.</param>
        /// <param name="min">Minimum inclusive bound.</param>
        /// <param name="max">Maximum inclusive bound.</param>
        /// <returns>A new QueryEngine with the filtered IQueryable.</returns>
        public CustomQueryableExtensions<T> WhereBetween(string propertyPath, object min, object max)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var left = BuildNestedPropertyExpression(parameter, propertyPath);
            var minConst = Expression.Constant(Convert.ChangeType(min, left.Type), left.Type);
            var maxConst = Expression.Constant(Convert.ChangeType(max, left.Type), left.Type);

            var ge = Expression.GreaterThanOrEqual(left, minConst);
            var le = Expression.LessThanOrEqual(left, maxConst);
            var body = Expression.AndAlso(ge, le);

            var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);
            return new CustomQueryableExtensions<T>(Source.Where(lambda));
        }

        /// <summary>
        /// Filters the query to include only those items where the property value is not null.
        /// Implements an expression equivalent to: x => x.Prop != null
        /// </summary>
        /// <param name="propertyPath">The dotted path to the property.</param>
        /// <returns>A new QueryEngine with the filtered IQueryable.</returns>
        public CustomQueryableExtensions<T> WhereNotNull(string propertyPath)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var prop = BuildNestedPropertyExpression(parameter, propertyPath);
            var notNull = Expression.NotEqual(prop, Expression.Constant(null, prop.Type));
            var lambda = Expression.Lambda<Func<T, bool>>(notNull, parameter);
            return new CustomQueryableExtensions<T>(Source.Where(lambda));
        }

        /// <summary>
        /// Filters items where the string property does NOT contain the given substring.
        /// </summary>
        public CustomQueryableExtensions<T> WhereNotContains(string propertyPath, string substring)
        {
            var positive = BuildStringMethodCall(propertyPath, nameof(string.Contains), substring);
            var negated = Expression.Lambda<Func<T, bool>>(
                Expression.Not(positive.Body),
                positive.Parameters
            );
            return new CustomQueryableExtensions<T>(Source.Where(negated));
        }

        /// <summary>
        /// Filters items where the string property does NOT start with the given prefix.
        /// </summary>
        public CustomQueryableExtensions<T> WhereNotStartsWith(string propertyPath, string prefix)
        {
            var positive = BuildStringMethodCall(propertyPath, nameof(string.StartsWith), prefix);
            var negated = Expression.Lambda<Func<T, bool>>(
                Expression.Not(positive.Body),
                positive.Parameters
            );
            return new CustomQueryableExtensions<T>(Source.Where(negated));
        }

        /// <summary>
        /// Filters items where the string property does NOT end with the given suffix.
        /// </summary>
        public CustomQueryableExtensions<T> WhereNotEndsWith(string propertyPath, string suffix)
        {
            var positive = BuildStringMethodCall(propertyPath, nameof(string.EndsWith), suffix);
            var negated = Expression.Lambda<Func<T, bool>>(
                Expression.Not(positive.Body),
                positive.Parameters
            );
            return new CustomQueryableExtensions<T>(Source.Where(negated));
        }

        /// <summary>
        /// Filters items where Math.Abs(<paramref name="propertyPath"/>) is less than <paramref name="threshold"/>.
        /// </summary>
        public CustomQueryableExtensions<T> WhereAbsoluteLessThan(string propertyPath, double threshold)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propExpr = BuildNestedPropertyExpression(parameter, propertyPath);

            // Math.Abs(prop)
            var absMethod = typeof(Math).GetMethod(nameof(Math.Abs), new[] { propExpr.Type })
                            ?? throw new InvalidOperationException("No Math.Abs overload for " + propExpr.Type);
            var absCall = Expression.Call(absMethod, propExpr);

            // Abs(prop) < threshold
            var threshConst = Expression.Constant(Convert.ChangeType(threshold, absCall.Type), absCall.Type);
            var comparison = Expression.LessThan(absCall, threshConst);

            var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
            return new CustomQueryableExtensions<T>(Source.Where(lambda));
        }

        /// <summary>
        /// Filters items where x.Code.Substring(startIndex, length) == <paramref name="match"/>.
        /// </summary>
        public CustomQueryableExtensions<T> WhereSubstringEquals(string propertyPath, int startIndex, int length, string match)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propExpr = BuildNestedPropertyExpression(parameter, propertyPath);
            var substrMethod = typeof(string).GetMethod(nameof(string.Substring), [typeof(int), typeof(int)])!;
            var startConst = Expression.Constant(startIndex);
            var lengthConst = Expression.Constant(length);
            var substrCall = Expression.Call(propExpr, substrMethod, startConst, lengthConst);
            var matchConst = Expression.Constant(match, typeof(string));
            var comparison = Expression.Equal(substrCall, matchConst);
            var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
            return new CustomQueryableExtensions<T>(Source.Where(lambda));
        }
        #endregion Filter

        #region Vector-Filter
        /// <summary>
        /// Filters items where the cosine distance between the vector property and <paramref name="queryVector"/> is less than <paramref name="threshold"/>.
        /// Note: This supports vector threshold filtering.
        /// </summary>
        public CustomQueryableExtensions<T> WhereVectorDistanceLessThan(Expression<Func<T, float[]>> vectorSelector, float[] queryVector, float threshold)
        {
            var parameter = vectorSelector.Parameters[0];
            var call = BuildCosineDistanceCall(vectorSelector.Body, queryVector);
            var comparison = Expression.LessThan(call, Expression.Constant(threshold));
            var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
            return new CustomQueryableExtensions<T>(Source.Where(lambda));
        }

        /// <summary>
        /// Applies an external full‑text score lookup and returns items ordered by that score.
        /// Example usage:
        ///   var ftIds = algolia.Query("foo");
        ///   var scores = algolia.GetScores(ftIds);
        ///   engine.ExternalFullTextSearch(ftIds, x => x.Id, scores);
        /// </summary>
        public IEnumerable<T> ExternalFullTextSearch<TKey>(IEnumerable<TKey> matchingKeys, Func<T, TKey> keySelector, IDictionary<TKey, float> externalScores)
        {
            var filtered = Source.AsEnumerable().Where(item => matchingKeys.Contains(keySelector(item)));
            return filtered.OrderByDescending(item => externalScores.TryGetValue(keySelector(item), out var s) ? s : 0f);
        }
        #endregion Vector-Filter

        #region Order
        /// <summary>
        /// Orders the query by the property at <paramref name="propertyPath"/> ascending or descending.
        /// </summary>
        public CustomQueryableExtensions<T> OrderBy(string propertyPath, bool descending = false)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = BuildNestedPropertyExpression(parameter, propertyPath);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = descending ? "OrderByDescending" : "OrderBy";
            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);

            var orderedQuery = (IOrderedQueryable<T>)method.Invoke(null, [Source, lambda])!;
            return new CustomQueryableExtensions<T>(orderedQuery);
        }

        /// <summary>
        /// Applies a subsequent ordering (ThenBy or ThenByDescending) on the query results based on a specified property.
        /// Must be called after an initial OrderBy or OrderByDescending.
        /// </summary>
        /// <param name="propertyPath">The dot-separated property name to order by.</param>
        /// <param name="descending">Whether to apply descending order. Defaults to false (ascending).</param>
        /// <returns>A new <see cref="CustomQueryableExtensions{T}"/> with the ordered query.</returns>
        /// <exception cref="InvalidOperationException">Thrown if called before an initial OrderBy or OrderByDescending.</exception>
        public CustomQueryableExtensions<T> ThenBy(string propertyPath, bool descending = false)
        {
            if (Source is not IOrderedQueryable<T> orderedSource)
                throw new InvalidOperationException("ThenBy can only be called after OrderBy.");

            var parameter = Expression.Parameter(typeof(T), "x");
            var property = BuildNestedPropertyExpression(parameter, propertyPath);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = descending ? "ThenByDescending" : "ThenBy";
            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);

            var newQuery = (IOrderedQueryable<T>)method.Invoke(null, new object[] { orderedSource, lambda })!;
            return new CustomQueryableExtensions<T>(newQuery);
        }

        /// <summary>
        /// Orders the query by cosine similarity between the vector property and <paramref name="queryVector"/>.
        /// Descending by default (closest vectors first).
        /// 
        /// Note: Weighted scoring or combined ranking is not supported inside the query.
        /// For composite scoring, fetch results and score manually in memory.
        /// </summary>
        public CustomQueryableExtensions<T> OrderByVectorSimilarity(
            Expression<Func<T, float[]>> vectorSelector,
            float[] queryVector,
            bool descending = true)
        {
            var parameter = vectorSelector.Parameters[0];
            var call = BuildCosineDistanceCall(vectorSelector.Body, queryVector);
            var lambda = Expression.Lambda<Func<T, float>>(call, parameter);

            var methodName = descending ? "OrderByDescending" : "OrderBy";
            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), typeof(float));

            var newQuery = (IOrderedQueryable<T>)method.Invoke(null, new object[] { Source, lambda })!;
            return new CustomQueryableExtensions<T>(newQuery);
        }

        /// <summary>
        /// Applies a ThenBy or ThenByDescending ordering by cosine similarity of the vector property
        /// to a query vector, chaining after a prior ordering.
        /// </summary>
        /// <param name="vectorSelector">Expression selecting the vector property.</param>
        /// <param name="queryVector">The query vector to compare against.</param>
        /// <param name="descending">Whether to order descending (default true).</param>
        /// <returns>New QueryEngine with ordered IQueryable supporting further chaining.</returns>
        /// <exception cref="InvalidCastException">Thrown if the underlying IQueryable is not IOrderedQueryable.</exception>
        public CustomQueryableExtensions<T> ThenByVectorSimilarity(
            Expression<Func<T, float[]>> vectorSelector,
            float[] queryVector,
            bool descending = true)
        {
            if (Source is not IOrderedQueryable<T> orderedSource)
                throw new InvalidCastException("ThenByVectorSimilarity requires the underlying source to be an IOrderedQueryable. Call OrderByVectorSimilarity first.");

            var parameter = vectorSelector.Parameters[0];
            var call = BuildCosineDistanceCall(vectorSelector.Body, queryVector);
            var lambda = Expression.Lambda<Func<T, float>>(call, parameter);

            var methodName = descending ? "ThenByDescending" : "ThenBy";
            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), typeof(float));

            var newQuery = (IOrderedQueryable<T>)method.Invoke(null, new object[] { orderedSource, lambda })!;
            return new CustomQueryableExtensions<T>(newQuery);
        }

        /// <summary>
        /// Performs an in-memory hybrid ranking by combining text relevance scores with vector similarity scores.
        /// 
        /// This method fetches all items from the current query source into memory, computes a combined score
        /// using a weighted sum of a user-provided text scoring function and cosine similarity to a query vector,
        /// then sorts the results by this hybrid score in descending order.
        /// 
        /// Use this when you need to perform composite ranking combining text relevance and vector similarity,
        /// which cannot be expressed as a single LINQ-to-SQL query.
        /// 
        /// <para>Parameters:</para>
        /// <list type="bullet">
        ///   <item><paramref name="textScore"/>: A function to compute the text-based relevance score of each item.</item>
        ///   <item><paramref name="vectorSelector"/>: An expression selecting the vector property from each item.</item>
        ///   <item><paramref name="queryVector"/>: The query vector to compare against each item's vector.</item>
        ///   <item><paramref name="textWeight"/>: Weight factor for the text score in the combined ranking (default 0.5).</item>
        ///   <item><paramref name="vectorWeight"/>: Weight factor for the vector similarity score in the combined ranking (default 0.5).</item>
        /// </list>
        /// 
        /// <para>Returns:</para>
        /// An IEnumerable of items ordered by the combined hybrid score descending.
        /// </summary>
        public IEnumerable<T> ToHybridRanking(Func<T, float> textScore, Expression<Func<T, float[]>> vectorSelector, float[] queryVector, float textWeight = 0.5f, float vectorWeight = 0.5f)
        {
            var vecFunc = vectorSelector.Compile();
            return Source
                .AsEnumerable()
                .Select(item => (
                    Item: item,
                    Score: textWeight * textScore(item) + vectorWeight * (1 - VectorMath.CosineDistance(vecFunc(item), queryVector))
                ))
                .OrderByDescending(p => p.Score)
                .Select(p => p.Item);
        }

        /// <summary>
        /// Orders the query results by a custom float scoring expression provided by the caller.
        /// 
        /// This method compiles the supplied <paramref name="scoreExpr"/> expression into a delegate,
        /// enumerates the query source in-memory, then sorts the items based on the computed scores.
        /// 
        /// This approach enables complex or composite scoring logic that cannot be translated into SQL,
        /// such as combining vector distances with metadata fields in custom formulas.
        /// Example usage: engine.OrderByScore(x => x.Popularity * 0.7f + (1 - VectorMath.CosineDistance(x.Vector, q)) * 0.3f)
        /// 
        /// <para>Parameters:</para>
        /// <list type="bullet">
        ///   <item><paramref name="scoreExpr"/>: An expression that computes a float score for each item.</item>
        ///   <item><paramref name="descending"/>: Whether to order descending (default true) or ascending.</item>
        /// </list>
        /// 
        /// <para>Returns:</para>
        /// An IEnumerable of items ordered by the custom score.
        /// </summary>
        public IEnumerable<T> OrderByScore(Expression<Func<T, float>> scoreExpr, bool descending = true)
        {
            var scorer = scoreExpr.Compile();
            var seq = Source.AsEnumerable();
            return descending
                ? seq.OrderByDescending(scorer)
                : seq.OrderBy(scorer);
        }
        #endregion Order

        #region Paginate
        /// <summary>
        /// Skips <paramref name="skip"/> items and takes <paramref name="take"/> items from the query.
        /// </summary>
        public CustomQueryableExtensions<T> Paginate(int skip, int take)
        {
            return new CustomQueryableExtensions<T>(Source.Skip(skip).Take(take));
        }
        #endregion Paginate

        #region Project
        /// <summary>
        /// Projects each item of type T into an <see cref="ExpandoObject"/> containing only the specified properties.
        /// Supports nested property paths (e.g., "Address.City").
        /// </summary>
        /// <param name="propertyPaths">The list of property paths to select.</param>
        /// <returns>An enumerable of <see cref="ExpandoObject"/> where each contains the requested fields and their values.</returns>
        /// <remarks>
        /// Useful for dynamic projections where you only want certain fields without creating a strongly-typed DTO.
        /// </remarks>
        public IEnumerable<ExpandoObject> SelectFields(params string[] propertyPaths)
        {
            var list = Source.AsEnumerable();
            foreach (var item in list)
            {
                IDictionary<string, object?> expando = new ExpandoObject();
                foreach (var path in propertyPaths)
                {
                    var parts = path.Split('.');
                    object? current = item;
                    foreach (var prop in parts)
                    {
                        if (current == null)
                        {
                            // If any intermediate property is null, break and assign null
                            current = null;
                            break;
                        }
                        var pi = current.GetType().GetProperty(prop);
                        if (pi == null)
                        {
                            // Property not found, assign null and break
                            current = null;
                            break;
                        }
                        current = pi.GetValue(current);
                    }
                    expando[path] = current;
                }
                yield return (ExpandoObject)expando;
            }
        }
        #endregion Project

        #region Group
        /// <summary>
        /// Groups the source items by the specified property path.
        /// Supports nested properties (e.g., "Category.Name").
        /// Example usage:
        ///   var groups = engine.GroupBy("Category.Name");
        ///   foreach (var g in groups) Console.WriteLine($"{g.Key}: {g.Count()}");
        /// </summary>
        /// <param name="propertyPath">The dot-separated property path to group by.</param>
        /// <returns>
        /// An <see cref="IQueryable{IGrouping}"/> where each grouping's key is the property value,
        /// boxed as <see cref="object"/>.
        /// </returns>
        /// <remarks>
        /// Note that the grouping key is cast to object, so value types will be boxed.
        /// Use caution with complex or large key types.
        /// </remarks>
        public IQueryable<IGrouping<object, T>> GroupBy(string propertyPath)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var keyExpr = BuildNestedPropertyExpression(parameter, propertyPath);
            var lambda = Expression.Lambda<Func<T, object>>(
                Expression.Convert(keyExpr, typeof(object)),
                parameter
            );

            return Source.GroupBy(lambda);
        }
        #endregion Group

        #region Aggregate
        /// <summary>
        /// Calculates the sum of a numeric property specified by <paramref name="propertyPath"/> over the source data.
        /// Supports common numeric types: int, long, float, double, decimal.
        /// Throws NotSupportedException if the type is not supported.
        /// </summary>
        /// <typeparam name="TResult">The numeric result type (int, long, float, double, decimal).</typeparam>
        /// <param name="propertyPath">Dot-separated property path to sum over.</param>
        /// <returns>The sum of the specified property for all elements in the source.</returns>
        public TResult Sum<TResult>(string propertyPath)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var prop = BuildNestedPropertyExpression(parameter, propertyPath);

            if (typeof(TResult) == typeof(int))
                return (TResult)(object)Source.Sum(Expression.Lambda<Func<T, int>>(prop, parameter));
            else if (typeof(TResult) == typeof(long))
                return (TResult)(object)Source.Sum(Expression.Lambda<Func<T, long>>(prop, parameter));
            else if (typeof(TResult) == typeof(float))
                return (TResult)(object)Source.Sum(Expression.Lambda<Func<T, float>>(prop, parameter));
            else if (typeof(TResult) == typeof(double))
                return (TResult)(object)Source.Sum(Expression.Lambda<Func<T, double>>(prop, parameter));
            else if (typeof(TResult) == typeof(decimal))
                return (TResult)(object)Source.Sum(Expression.Lambda<Func<T, decimal>>(prop, parameter));
            else
                throw new NotSupportedException($"Sum for type {typeof(TResult).Name} is not supported.");
        }

        /// <summary>
        /// Calculates the average of a numeric property specified by <paramref name="propertyPath"/> over the source data.
        /// Supports common numeric types: int, long, float, double, decimal.
        /// Throws NotSupportedException if the type is not supported.
        /// </summary>
        /// <typeparam name="TResult">The numeric result type (int, long, float, double, decimal).</typeparam>
        /// <param name="propertyPath">Dot-separated property path to average over.</param>
        /// <returns>The average of the specified property for all elements in the source.</returns>
        public TResult Average<TResult>(string propertyPath)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var prop = BuildNestedPropertyExpression(parameter, propertyPath);

            if (typeof(TResult) == typeof(int))
                return (TResult)(object)Source.Average(Expression.Lambda<Func<T, int>>(prop, parameter));
            else if (typeof(TResult) == typeof(long))
                return (TResult)(object)Source.Average(Expression.Lambda<Func<T, long>>(prop, parameter));
            else if (typeof(TResult) == typeof(float))
                return (TResult)(object)Source.Average(Expression.Lambda<Func<T, float>>(prop, parameter));
            else if (typeof(TResult) == typeof(double))
                return (TResult)(object)Source.Average(Expression.Lambda<Func<T, double>>(prop, parameter));
            else if (typeof(TResult) == typeof(decimal))
                return (TResult)(object)Source.Average(Expression.Lambda<Func<T, decimal>>(prop, parameter));
            else
                throw new NotSupportedException($"Average for type {typeof(TResult).Name} is not supported.");
        }

        /// <summary>
        /// Returns the count of elements in the source.
        /// </summary>
        /// <returns>The number of elements.</returns>
        public int Count() => Source.Count();
        #endregion Aggregate

        #region Distinct
        /// <summary>
        /// Returns a sequence of distinct elements from the source, comparing based on the specified property path.
        /// This is similar to SQL's "DISTINCT ON" for a specific field or nested field.
        ///
        /// Example:
        ///   engine.DistinctBy("Category.Name") — returns one item per unique category name.
        ///
        /// Notes:
        /// - Uses the value of the given property (supports nested like "Address.City") as the distinct key.
        /// - Uses `object.Equals` for key comparison (null-safe).
        /// </summary>
        /// <param name="propertyPath">The dot-separated path of the property to group distinct elements by.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> containing the first item for each unique key.</returns>
        public IEnumerable<T> DistinctBy(string propertyPath)
        {
            var keySelector = Expression.Lambda<Func<T, object>>(
              Expression.Convert(BuildNestedPropertyExpression(Expression.Parameter(typeof(T), "x"), propertyPath), typeof(object)),
              Expression.Parameter(typeof(T), "x")
            ).Compile();
            return Source.AsEnumerable().GroupBy(keySelector).Select(g => g.First());
        }
        #endregion Distinct

        #region Execute
        /// <summary>
        /// Builds the final IQueryable query.
        /// </summary>
        public IQueryable<T> Build() => Source;
        #endregion Execute

        #region Helpers
        /// <summary>
        /// Safety check for a property
        /// </summary>
        private static void EnsurePropertyType(Expression propExpr, string expectedDescription, params Type[] validTypes)
        {
            if (!validTypes.Contains(propExpr.Type)) throw new InvalidOperationException($"Invalid property type '{propExpr.Type}'. Expected: {expectedDescription}.");
        }

        /// <summary>
        /// Safety check when comparing properties
        /// </summary>
        private static bool IsComparableType(Type type)
        {
            return typeof(IComparable).IsAssignableFrom(type) || type.IsGenericType && typeof(IComparable<>).MakeGenericType(type).IsAssignableFrom(type);
        }

        /// <summary>
        /// Filters string properties containing the specified case insensitive substring.
        /// </summary>
        private static Expression<Func<T, bool>> BuildCaseInsensitiveContains(string path, string term)
        {
            var p = Expression.Parameter(typeof(T), "x");
            var prop = BuildNestedPropertyExpression(p, path);
            var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
            var left = Expression.Call(prop, toLower);
            var right = Expression.Constant(term.ToLower());
            var contains = Expression.Call(left, typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!, right);
            return Expression.Lambda<Func<T, bool>>(contains, p);
        }

        /// <summary>
        /// Where the Date component of a DateTime property matches a given DateTime date
        /// </summary>
        private static Expression<Func<T, bool>> BuildDateEquals(string path, DateTime date)
        {
            var p = Expression.Parameter(typeof(T), "x");
            var prop = BuildNestedPropertyExpression(p, path);
            var dateProp = Expression.Property(prop, nameof(DateTime.Date));
            var target = Expression.Constant(date.Date, typeof(DateTime));
            var eq = Expression.Equal(dateProp, target);
            return Expression.Lambda<Func<T, bool>>(eq, p);
        }

        /// <summary>
        /// Builds a null-safe equality expression for a nested property path.
        /// 
        /// This method generates a predicate of the form:
        /// 
        /// (x.Prop1 != null && x.Prop1.Prop2 != null && … && x.PropN == value)
        /// 
        /// ensuring that all intermediate properties in the path are checked for null
        /// before comparing the final property to the specified value. This prevents
        /// NullReferenceExceptions during query evaluation in LINQ providers.
        /// 
        /// Example:
        /// For propertyPath = "Address.City.Name" and value = "Seattle",
        /// it generates:
        /// (x.Address != null && x.Address.City != null && x.Address.City.Name == "Seattle")
        /// 
        /// This is especially useful when querying against nested objects where any
        /// intermediate object could be null.
        /// </summary>
        /// <param name="propertyPath">Dot-separated nested property path</param>
        /// <param name="value">Value to compare for equality</param>
        /// <returns>A lambda expression for use in IQueryable filters</returns>
        // A helper that builds x => x.Prop1 != null && x.Prop1.Prop2 != null && x.Prop1.Prop2.Prop3 == value
        private static Expression<Func<T, bool>> BuildNullSafeEquals(string propertyPath, object value)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            Expression current = parameter;
            Expression nullChecks = null!;
            foreach (var prop in propertyPath.Split('.'))
            {
                var next = Expression.PropertyOrField(current, prop);
                var notNull = Expression.NotEqual(current, Expression.Constant(null, current.Type));
                nullChecks = nullChecks is null ? notNull : Expression.AndAlso(nullChecks, notNull);
                current = next;
            }
            var right = Expression.Constant(Convert.ChangeType(value, current.Type), current.Type);
            var comparison = Expression.Equal(current, right);
            var body = nullChecks is null ? comparison : Expression.AndAlso(nullChecks, comparison);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        /// <summary>
        /// Helper methods for building expression trees used internally for filtering and ordering.
        /// Includes support for nested property access, comparison expressions, string method calls, and vector math calls.
        /// </summary>
        private static Expression BuildNestedPropertyExpression(Expression parameter, string propertyPath)
        {
            Expression current = parameter;
            foreach (var prop in propertyPath.Split('.'))
                current = Expression.PropertyOrField(current, prop);
            return current;
        }

        /// <summary>
        /// Builds a binary comparison expression (e.g., Equal, GreaterThan) for the property at <paramref name="propertyPath"/>.
        /// Handles conversion of <paramref name="value"/> to the property type.
        /// </summary>
        private static Expression<Func<T, bool>> BuildComparisonExpression(string propertyPath, ExpressionType op, object value)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var left = BuildNestedPropertyExpression(parameter, propertyPath);

            // Convert value to proper type
            var right = Expression.Constant(Convert.ChangeType(value, left.Type), left.Type);
            var comparison = Expression.MakeBinary(op, left, right);

            return Expression.Lambda<Func<T, bool>>(comparison, parameter);
        }

        /// <summary>
        /// Builds a call expression for string methods (Contains, StartsWith, EndsWith) on the property at <paramref name="propertyPath"/>.
        /// Throws if the property is not a string.
        /// </summary>
        private static Expression<Func<T, bool>> BuildStringMethodCall(string propertyPath, string methodName, string value)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = BuildNestedPropertyExpression(parameter, propertyPath);
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) });

            if (property.Type != typeof(string))
                throw new InvalidOperationException($"Property '{propertyPath}' is not a string.");

            var call = Expression.Call(property, method!, Expression.Constant(value));
            return Expression.Lambda<Func<T, bool>>(call, parameter);
        }

        /// <summary>
        /// Builds a method call expression invoking VectorMath.CosineDistance on the left vector property and the constant right vector.
        /// Used to filter or order by cosine distance.
        /// </summary>
        private static MethodCallExpression BuildCosineDistanceCall(Expression leftVectorExpr, float[] rightVector)
        {
            var method = typeof(VectorMath).GetMethod(nameof(VectorMath.CosineDistance), new[] { typeof(float[]), typeof(float[]) })!;
            return Expression.Call(method, leftVectorExpr, Expression.Constant(rightVector));
        }
        #endregion Helpers
    }

    #region Custom-Visitors
    /// <summary>
    /// Expression visitor that simplifies negated expressions like:
    /// - !(a == b) → a != b
    /// - !(a != b) → a == b
    /// - !(a > b) → a <= b
    /// - !(a && b) → !a || !b
    /// - !(a || b) → !a && !b
    /// Useful for normalizing filters in query builders.
    /// </summary>
    public class SimplifyNotVisitor : ExpressionVisitor
    {
        /// <summary>
        /// Visits unary expressions like `!expr` and simplifies where possible.
        /// </summary>
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Not)
            {
                var operand = Visit(node.Operand); // visit inner first

                // Handle double negation: !!a → a
                if (operand.NodeType == ExpressionType.Not)
                    return Visit(((UnaryExpression)operand).Operand);

                // Negate binary comparisons
                if (operand is BinaryExpression bin)
                {
                    if (_negatedComparisons.TryGetValue(bin.NodeType, out var opposite))
                    {
                        return Expression.MakeBinary(opposite, bin.Left, bin.Right);
                    }

                    // Apply De Morgan’s law: !(A && B) → !A || !B, !(A || B) → !A && !B
                    if (bin.NodeType == ExpressionType.AndAlso || bin.NodeType == ExpressionType.OrElse)
                    {
                        var leftNot = Expression.Not(bin.Left);
                        var rightNot = Expression.Not(bin.Right);
                        var deMorganOp = bin.NodeType == ExpressionType.AndAlso
                            ? ExpressionType.OrElse
                            : ExpressionType.AndAlso;

                        return Visit(Expression.MakeBinary(deMorganOp, leftNot, rightNot));
                    }
                }

                // Negate constants: !true → false, !false → true
                if (operand is ConstantExpression constExpr && constExpr.Type == typeof(bool))
                {
                    var negated = !(bool)constExpr.Value!;
                    return Expression.Constant(negated);
                }

                return Expression.Not(operand); // fallback to normal NOT
            }

            return base.VisitUnary(node);
        }

        /// <summary>
        /// Optional: Also simplify nested binary expressions (not always needed).
        /// </summary>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // Visit sub-expressions
            var left = Visit(node.Left);
            var right = Visit(node.Right);

            return node.Update(left, node.Conversion, right);
        }

        /// <summary>
        /// Mapping of comparison operators to their logical opposites.
        /// Used to simplify !(a == b) → a != b and so on.
        /// </summary>
        private static readonly Dictionary<ExpressionType, ExpressionType> _negatedComparisons = new()
        {
            { ExpressionType.Equal, ExpressionType.NotEqual },
            { ExpressionType.NotEqual, ExpressionType.Equal },
            { ExpressionType.GreaterThan, ExpressionType.LessThanOrEqual },
            { ExpressionType.LessThan, ExpressionType.GreaterThanOrEqual },
            { ExpressionType.GreaterThanOrEqual, ExpressionType.LessThan },
            { ExpressionType.LessThanOrEqual, ExpressionType.GreaterThan }
        };
    }
    #endregion Custom-Visitors

    #region Static Helpers
    /// <summary>
    /// Provides vector math utilities.
    /// </summary>
    public static class VectorMath
    {
        /// <summary>
        /// Calculates the cosine distance between two vectors.
        /// Returns float.MaxValue if inputs are null or lengths mismatch.
        /// </summary>
        public static float CosineDistance(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return float.MaxValue;

            float dot = 0f, magA = 0f, magB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }

            float denom = (float)(Math.Sqrt(magA) * Math.Sqrt(magB));
            return denom == 0 ? 1f : 1f - dot / denom;
        }
    }
    #endregion Static Helpers

    /*
     =================
     = SUMMARY TABLE =
     =================

     ----------------------------------------------------------------------------------------------------------
     Feature                           | Supported? | Notes
     ----------------------------------|------------|----------------------------------------------------------
     Range filtering                   | ✅         | WhereGreaterThan, WhereLessThan
     String match (Contains)           | ✅         | WhereContains, StartsWith, EndsWith
     Negated string filters            | ✅         | WhereNotContains, WhereNotStartsWith, WhereNotEndsWith
     Vector threshold filter           | ✅         | WhereVectorDistanceLessThan
     Hybrid search (text + vector)     | ✅         | ToHybridRanking in-memory fusion of text & vector scores
     Weighted scoring                  | ✅         | OrderByScore supports arbitrary composite scoring
     Metadata scoring / boosting       | ✅         | Can boost via OrderByScore combining metadata fields
     Custom ranking expressions        | ✅         | OrderByScore DSL for any float expression
     Inline method calls               | ✅         | WhereAbsoluteLessThan, WhereSubstringEquals
     Pagination                        | ✅         | Paginate
     Ordering by properties            | ✅         | OrderBy
     Ordering by vector similarity     | ✅         | OrderByVectorSimilarity
     More filters (In/Between/NotNull) | ✅         | WhereIn, WhereBetween, WhereNotNull
     Full-text search (external hook)  | ✅         | ExternalFullTextSearch integrates external FT engines
     Projection (SelectFields)         | ✅         | SelectFields for dynamic DTO shaping
     Grouping                          | ✅         | GroupBy("Category.Name")
     Aggregates (Count/Sum/Average)    | ✅         | Count(), Sum(), Average()
     ----------------------------------------------------------------------------------------------------------
     */
}