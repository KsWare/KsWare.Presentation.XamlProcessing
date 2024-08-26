using System;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace KsWare.Presentation.XamlProcessing {

	public static class XElementXamlExtensions {

		/// <summary>
		/// Gets the CLR type of the object represented by the element.
		/// </summary>
		/// <param name="xamlElement">The xaml element.</param>
		/// <returns>The <see cref="Type"/>.</returns>
		/// <remarks><c>&lt;Button></c> returns typeof(Button)<br/>
		/// <c>&lt;s:Int32></c> returns typeof(Int32)<br/>
		/// <c>&lt;Button.Foreground></c> returns typeof(Brush)<br/>
		/// </remarks>
		/// <exception cref="System.ArgumentException">Type could not be resolved</exception>
		[SuppressMessage("ReSharper", "UnusedMember.Global")]
		public static Type GetClrType(this XElement xamlElement) 
			=> XamlUtils.GetRepresentingType(xamlElement, true)!;
	
	}

}
