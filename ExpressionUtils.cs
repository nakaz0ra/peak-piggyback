using System;
using System.Linq.Expressions;

namespace Piggyback
{
    public static class ExpressionUtils
    {
        public static Func<T, TResult> CreateFieldGetter<T, TResult>(string fieldName)
        {
            var param = Expression.Parameter(typeof(T), "instance");
            var field = Expression.Field(param, fieldName);
            var lambda = Expression.Lambda<Func<T, TResult>>(field, param);
            return lambda.Compile();
        }
    }
}
