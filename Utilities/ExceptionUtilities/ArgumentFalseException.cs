using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Omni_MVC_2.Utilities.ExceptionUtilities
{
    public class ArgumentFalseException : ArgumentException
    {
        public ArgumentFalseException(string? paramName) : base(paramName) { }

        /// <summary>
        /// Example Usage: ArgumentFalseException.ThrowIfFalse(result != null, "No records found");
        /// </summary>
        /// <param name="argument"> The boolean condition to evaluate. If false, the exception is thrown. </param>
        /// <param name="paramName"> The expression representing the argument. This is automatically populated by the compiler using <see cref="CallerArgumentExpressionAttribute"/>.
        /// </param>
        public static void ThrowIfFalse(bool argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (!argument) Throw(paramName);
        }

        [DoesNotReturn]
        internal static void Throw(string? paramName) => throw new ArgumentFalseException(paramName);
    }
}