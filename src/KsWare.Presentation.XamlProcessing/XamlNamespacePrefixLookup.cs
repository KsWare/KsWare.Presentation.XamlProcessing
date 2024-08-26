using System.Collections.Generic;
using System.Xaml;

namespace KsWare.Presentation.XamlProcessing {

	public class XamlNamespacePrefixLookup : INamespacePrefixLookup {

		public static readonly INamespacePrefixLookup Default = new XamlNamespacePrefixLookup();

		private readonly Dictionary<string, string> _namespacePrefixMap = new Dictionary<string, string>() {
			{"http://schemas.microsoft.com/winfx/2006/xaml/presentation", ""},
			{"http://schemas.microsoft.com/winfx/2006/xaml", "x"},
			{"clr-namespace:System;assembly=System.Runtime", "s"}
		};

		public string? LookupPrefix(string ns) {
			return _namespacePrefixMap.TryGetValue(ns, out var prefix) 
				? prefix 
				: null;
		}

	}

}