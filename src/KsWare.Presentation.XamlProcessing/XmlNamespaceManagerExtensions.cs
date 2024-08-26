using System.Collections.Generic;
using System.Threading;
using System.Xml;

namespace KsWare.Presentation.XamlProcessing {

    public static class XmlNamespaceManagerExtensions {

	    private static readonly ThreadLocal<string> _lastAddedPrefix=new ThreadLocal<string>();

	    public static void InitXamlNamespace(this XmlNamespaceManager nsManager, bool addCommonNamespaces=false) {
		    nsManager.AddNamespace("","http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			if(!addCommonNamespaces) return;
		    nsManager.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");
		    nsManager.AddNamespace("d", "http://schemas.microsoft.com/expression/blend/2008");
		    nsManager.AddNamespace("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
			#if NETFRAMEWORK
		    nsManager.AddNamespace("s","clr-namespace:System;assembly=mscorlib");
			#elif NET
		    nsManager.AddNamespace("s","clr-namespace:System;assembly=System.Runtime");
			#endif
		    nsManager.AddNamespace("i","http://schemas.microsoft.com/expression/2010/interactivity");
		    nsManager.AddNamespace("b","http://schemas.microsoft.com/xaml/behaviors");
		    nsManager.AddNamespace("prism","http://prismlibrary.com");
	    }

	    public static void TryAddNamespace(this XmlNamespaceManager nsManager, string prefix, string uri, out string usedPrefix) {
		    var p0 = nsManager.LookupPrefix(uri);
		    if(p0!=null) {usedPrefix = p0; return;}
		    if(p0==prefix) {usedPrefix = prefix; return;}

		    var c = 1;
		    usedPrefix = prefix;
		    while (nsManager.LookupNamespace(prefix)!=null) usedPrefix = $"{prefix}{(c++)}";
		    nsManager.AddNamespace(usedPrefix,uri);
	    }

	    public static void SetLastAddedPrefix(this XmlNamespaceManager nsManager, string prefix) {
//		    var holder = LastAddedPrefixes.GetOrCreateValue(nsManager); holder.LastPrefix = prefix;
		    _lastAddedPrefix.Value = prefix;
	    }

	    public static string? GetLastAddedPrefix(this XmlNamespaceManager nsManager) {
//		    return LastAddedPrefixes.TryGetValue(nsManager, out var holder) ? holder.LastPrefix : null;
		    return _lastAddedPrefix.IsValueCreated ? _lastAddedPrefix.Value : null;
	    }

		public static IEnumerable<string> Prefixes(this XmlNamespaceManager nsManager, bool includeDefault = false) {
			foreach (var prefix in nsManager)
				if (includeDefault || !string.IsNullOrEmpty(prefix.ToString()))
					yield return prefix.ToString();
		}

	    private class PrefixHolder {
		    public string? LastPrefix { get; set; }
	    }
    }
}
