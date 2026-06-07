using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Repository.DynamicQueries;

/// <summary>
/// A secure, reflection-based Expression Tree builder for dynamic LINQ queries.
/// It strictly validates field names to prevent injection attacks and handles type conversion.
/// </summary>
public static class ExpressionBuilder
{
    public static IQueryable<T> ApplyFilters<T>(IQueryable<T> query, List<FilterCriteria> filters)
    {
        if (filters == null || !filters.Any())
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Operator) || filter.Value == null)
                continue;

            // 1. Validate property safely using case-insensitive reflection
            var property = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.Name.Equals(filter.Field, StringComparison.OrdinalIgnoreCase));

            if (property == null)
                continue; // Prevent injection or crash on invalid fields

            var memberAccess = Expression.MakeMemberAccess(parameter, property);
            
            // 2. Parse value safely
            object? typedValue;
            try
            {
                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                
                if (targetType == typeof(Guid))
                    typedValue = Guid.Parse(filter.Value);
                else if (targetType.IsEnum)
                    typedValue = Enum.Parse(targetType, filter.Value, true);
                else
                    typedValue = Convert.ChangeType(filter.Value, targetType);
            }
            catch
            {
                continue; // Ignore invalid formats instead of crashing
            }

            // Convert to the nullable type if the property is nullable to match expression types safely
            var constant = Expression.Constant(typedValue, property.PropertyType);

            // 3. Apply operators securely
            Expression comparison;
            switch (filter.Operator.ToLower())
            {
                case "eq":
                    comparison = Expression.Equal(memberAccess, constant);
                    break;
                case "neq":
                    comparison = Expression.NotEqual(memberAccess, constant);
                    break;
                case "gt":
                    comparison = Expression.GreaterThan(memberAccess, constant);
                    break;
                case "lt":
                    comparison = Expression.LessThan(memberAccess, constant);
                    break;
                case "contains":
                    if (property.PropertyType == typeof(string))
                    {
                        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                        
                        var lowerMember = Expression.Call(memberAccess, toLowerMethod!);
                        var lowerConstant = Expression.Call(constant, toLowerMethod!);
                        
                        comparison = Expression.Call(lowerMember, containsMethod!, lowerConstant);
                    }
                    else
                    {
                        comparison = Expression.Equal(memberAccess, constant); // Fallback for non-strings
                    }
                    break;
                default:
                    comparison = Expression.Equal(memberAccess, constant);
                    break;
            }

            // 4. Combine expressions with AND logic
            combined = combined == null ? comparison : Expression.AndAlso(combined, comparison);
        }

        if (combined != null)
        {
            var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
            return query.Where(lambda);
        }

        return query;
    }
}
