using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Xml;

namespace KsWare.Presentation.XamlProcessing {

	/// <summary>
	/// Manages XML namespaces for XAML processing, extending the <see cref="XmlNamespaceManager"/>.
	/// </summary>
	[SuppressMessage("ReSharper", "HollowTypeName")]
	public class XamlNamespaceManager : XmlNamespaceManager {
		
		private static readonly ThreadLocal<string?> s_lastAddedPrefix=new ThreadLocal<string?>();

		/// <summary>
		/// Gets the common namespaces.
		/// </summary>
		/// <value>The common namespaces.</value>
		public Dictionary<string,string> CommonNamespaces {get;} =
			new Dictionary<string, string> {
				{ "x", "http://schemas.microsoft.com/winfx/2006/xaml" },
				{"d", "http://schemas.microsoft.com/expression/blend/2008"},
				{"mc", "http://schemas.openxmlformats.org/markup-compatibility/2006"},
#if NETFRAMEWORK
				{"s","clr-namespace:System;assembly=mscorlib"},
#elif NET
				{"s","clr-namespace:System;assembly=System.Runtime"},
#endif
				{"i","http://schemas.microsoft.com/expression/2010/interactivity"},
				{"b","http://schemas.microsoft.com/xaml/behaviors"},
				{"prism","http://prismlibrary.com"}
			};

		/// <summary>
		/// Initializes a new instance of the <see cref="XamlNamespaceManager"/> class.
		/// </summary>
		public XamlNamespaceManager() : base(new NameTable()) {
			AddNamespace("","http://schemas.microsoft.com/winfx/2006/xaml/presentation");
		}

		/// <summary>
		/// Initializes the common namespaces for XAML.
		/// </summary>
		/// <returns>XmlNamespaceManager.</returns>
		public XmlNamespaceManager InitCommonNamespaces() {
			foreach (var ns in CommonNamespaces) 
				AddNamespace(ns.Key,ns.Value);
			return this;
		}

		/// <summary>
		/// Attempts to add a namespace with the specified prefix and URI.
		/// </summary>
		/// <param name="suggestedPrefix">The suggested prefix for the namespace.</param>
		/// <param name="uri">The URI of the namespace.</param>
		/// <param name="usedPrefix">
		/// When this method returns, contains the prefix that was actually used. 
		/// This will be the suggested prefix if it was available, or a modified version of it if it was not.
		/// </param>
		/// <remarks>
		/// If the suggested prefix is already in use, this method will append a number to the prefix to make it unique.
		/// </remarks>
		public void TryAddNamespace(string suggestedPrefix, string uri, out string usedPrefix) {
			var p0 = LookupPrefix(uri);
			if(p0!=null) {usedPrefix = p0; return;}
			if(p0==suggestedPrefix) {usedPrefix = suggestedPrefix; return;}

			var c = 1;
			usedPrefix = suggestedPrefix;
			while (LookupNamespace(suggestedPrefix)!=null) usedPrefix = $"{suggestedPrefix}{(c++)}";
			AddNamespace(usedPrefix,uri);
		}

		/// <summary>
		/// [EXPERIMENTAL] Gets or sets the last added prefix.
		/// </summary>
		/// <value>The last added prefix.</value>
		public string? LastAddedPrefix {
			get => s_lastAddedPrefix.IsValueCreated ? s_lastAddedPrefix.Value : null;
			set => s_lastAddedPrefix.Value = value;
		}

		public IEnumerable<string> GetPrefixes(bool includeDefault = false) {
			foreach (var prefix in this)
				if (includeDefault || !string.IsNullOrEmpty(prefix.ToString()))
					yield return prefix.ToString();
		}

	}
}
