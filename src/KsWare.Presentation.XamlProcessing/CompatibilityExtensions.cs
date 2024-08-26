using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KsWare.Presentation.XamlProcessing {

#if NETFRAMEWORK
	public static class CompatibilityExtensions {

		/// <summary>
		/// Determines whether the current type can be assigned to a variable of the specified <paramref name="targetType"/>.
		/// </summary>
		/// <param name="this"></param>
		/// <param name="targetType">The type to compare with the current type.</param>
		/// <returns><c>true</c> if any of the following conditions is true:
		/// <list type="bullet">
		///   <item> The current instance and targetType represent the same type.</item>
		///   <item> The current type is derived either directly or indirectly from targetType. The current type is derived directly from targetType if it inherits from targetType; the current type is derived indirectly from targetType if it inherits from a succession of one or more classes that inherit from targetType.</item>
		///   <item> targetType is an interface that the current type implements.</item>
		///   <item> The current type is a generic type parameter, and targetType represents one of the constraints of the current type.</item>
		///   <item> The current type represents a value type, and targetType represents <c>Nullable&lt;c&gt;</c> (<c>Nullable(Of c)</c> in Visual Basic).</item>
		/// </list>
		/// <c>false</c> if none of these conditions are true, or if targetType is <c>null</c>.
		/// </returns>
		public static bool IsAssignableTo(this Type @this, Type? targetType) 
			=> targetType?.IsAssignableFrom(@this) ?? false;
	}
#endif
}
