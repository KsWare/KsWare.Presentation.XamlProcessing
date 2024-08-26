using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Baml2006;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xaml;
using System.Xaml.Schema;
using System.Xml;
using System.Xml.Linq;
using XamlReader = System.Windows.Markup.XamlReader;
using XamlWriter = System.Windows.Markup.XamlWriter;

namespace KsWare.Presentation.XamlProcessing {

	public class XamlUtils {

		public static Type ResolveXamlType(string xamlTypeName, XmlNamespaceManager nsManager) {
//		if (xamlTypeName.StartsWith("{") && xamlTypeName.EndsWith("}")) { // x:Static
//			var markupExtension = MarkupExtensionParser.ParseMarkupExtension<StaticExtension>(xamlTypeName);
//			if (markupExtension != null && markupExtension.MemberType != null) {
//				return markupExtension.MemberType;
//			}
//		}
			if (xamlTypeName.StartsWith("{x:Type ")) {
				xamlTypeName = xamlTypeName.Substring("{x:Type ".Length).TrimEnd('}');
			}

			var fragments = xamlTypeName.Split(':');
			string ns;
			string prefix;
			string name;
			if (fragments.Length == 2) {
				prefix = fragments[0];
				name = fragments[1];
				ns = nsManager.LookupNamespace(prefix);
			}
			else {
				prefix = "";
				name = fragments[0];
				ns = nsManager.DefaultNamespace;
			}
			var context = new XamlSchemaContext();
			var xamlType = context.GetXamlType(new XamlTypeName(ns, name));
			if (xamlType != null) return xamlType.UnderlyingType;

			throw new TypeLoadException($"The type '{xamlTypeName}' could not be resolved.");
		}

		public static string TryConvertToStatic(string componentKeyString, XmlNamespaceManager nsManager) {
			if (!componentKeyString.StartsWith("{ComponentResourceKey")) return componentKeyString;
			var me = MarkupExtensionParser.ParseMarkupExtension<ComponentResourceKey>(componentKeyString, nsManager);
			if (me.TypeInTargetAssembly.GetProperty(componentKeyString, BindingFlags.Public | BindingFlags.Static) == null)
				return componentKeyString;
			return $"{{x:Static {me.TypeInTargetAssembly.Name}.{me.ResourceId}}}";
			// {ComponentResourceKey TypeInTargetAssembly=DataGrid, ResourceId=FocusBorderBrushKey}
			// {x:Static SystemColors.HighlightBrushKey}
			// Only internal used: ThemeResourceKey, TemplateKey, SystemResourceKey
		}

		public static XElement FindResourceDirectoryElement(XElement resourceDictionaryElement, object key, Type type) {
			if (key == null) throw new ArgumentNullException(nameof(key));
//		if(resourceDictionaryElement.Name.LocalName!="ResourceDictionary") throw new ArgumentException("Not a ResourceDictionary element.")
			string keyString = GetResourceKeyString(key, CreateNamespaceManager(resourceDictionaryElement));

			return resourceDictionaryElement.Descendants().FirstOrDefault(e => {
				var xKeyAttribute = e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"));
				if (xKeyAttribute != null && xKeyAttribute.Value == keyString) return true;
				var targetTypeAttribute = e.Attribute("TargetType");
				return targetTypeAttribute != null && targetTypeAttribute.Value.Contains(type.Name);
			}) ?? throw new InvalidOperationException("Matching node not found.");
		}

		public static XmlNamespaceManager CreateNamespaceManager(XElement context) {
			var nsManager = new XmlNamespaceManager(new NameTable());
//		nsManager.AddNamespace("","http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			var element = context;
			while (element != null) {
				foreach (var attr in element.Attributes()) {
					if (!attr.IsNamespaceDeclaration) continue;
					var prefix = attr.Name.Namespace == XNamespace.None ? string.Empty : attr.Name.LocalName;
					if (prefix == "" || nsManager.LookupNamespace(prefix) == null)
						nsManager.AddNamespace(prefix, attr.Value);
				}
				element = element.Parent;
			}
			return nsManager;
		}

		public static XmlNamespaceManager CreateNamespaceManager(XElement context, XmlNamespaceManager existingNsManager) {
			var nsManager = new XmlNamespaceManager(new NameTable());
			foreach (var prefix in existingNsManager) {
				if (new[] {"xmlns", "xml"}.Contains(prefix.ToString())) continue;
				var ns = existingNsManager.LookupNamespace(prefix.ToString());
				if (!string.IsNullOrEmpty(ns)) nsManager.AddNamespace(prefix.ToString(), ns);
			}
			foreach (var attr in context?.Attributes() ?? Array.Empty<XAttribute>()) {
				if (!attr.IsNamespaceDeclaration) continue;
				var prefix = attr.Name.Namespace == XNamespace.None ? string.Empty : attr.Name.LocalName;
				if (nsManager.LookupNamespace(prefix) == null)
					nsManager.AddNamespace(prefix, attr.Value);
			}
			return nsManager;
		}

		public static string GetQualifiedName(Type type, XmlNamespaceManager nsManager)
			=> GetQualifiedName(type, null, nsManager, allowAddNamespace: false);

		public static string CreateQualifiedName(Type type, XmlNamespaceManager nsManager)
			=> GetQualifiedName(type, null, nsManager, true);

		private static string GetQualifiedName(Type type, XElement? context, XmlNamespaceManager? nsManager,
			bool allowAddNamespace) {
			if (context == null && nsManager == null) throw new ArgumentException();
			if (nsManager == null) nsManager = CreateNamespaceManager(context);
			else nsManager = CreateNamespaceManager(context, nsManager);
			var xmlNamespace = FindXmlnsDefinition(type);
			var prefix = nsManager.LookupPrefix(xmlNamespace);
			if (prefix != null) return string.IsNullOrEmpty(prefix) ? type.Name : $"{prefix}:{type.Name}";
//			foreach (var p in nsManager) {
//				Debug.WriteLine($"{p} {nsManager.LookupNamespace(p.ToString())}");
//			}
			if (!allowAddNamespace) throw new ArgumentException("Type namespace not registered!");

			switch (xmlNamespace) {
				case "clr-namespace:System;assembly=System.Runtime": prefix = "s"; break;
				case "clr-namespace:System;assembly=System.Private.CoreLib": prefix = "s"; break;
				case "clr-namespace:System;assembly=mscorlib": prefix = "s"; break;
				default:
					prefix = FindXmlnsPrefix(type);
					if (prefix == null) {
						prefix = xmlNamespace.Contains("clr-namespace")
							? (type.Namespace ?? type.Assembly.GetName(false).Name ?? "ns").Split('.').Last().ToLower()
							: xmlNamespace.TrimEnd('/').Split('.', '/').Last();
						prefix = string.Concat(prefix.Where(char.IsLetterOrDigit));
					}
					break;
			}

			while ((nsManager.LookupNamespace(prefix)) != null) {
				var match = Regex.Match(prefix, @"^(?<name>.*?)(?<number>\d+)$");
				if (match.Success) prefix = match.Groups["name"].Value + int.Parse(match.Groups["number"].Value) + 1;
				else prefix = prefix + "1";
			}

			nsManager.AddNamespace(prefix, xmlNamespace);
			nsManager.SetLastAddedPrefix(prefix);
			return string.IsNullOrEmpty(prefix) ? type.Name : $"{prefix}:{type.Name}";
		}

		public static string FindXmlnsDefinition(Type type) {
			if (type == null) throw new ArgumentNullException(nameof(type));

			// [assembly: XmlnsDefinition(XmlNamespace, "KsWare.Presentation.Core.Utils")]

			var attr = type.Assembly.GetCustomAttributes(typeof(XmlnsDefinitionAttribute), false)
				.Cast<XmlnsDefinitionAttribute>()
				.FirstOrDefault(a => a.ClrNamespace == type.Namespace);

			if (attr != null) return attr.XmlNamespace;

			var assemblyName = type.Assembly.GetName().Name;

			// in XAML "System.Runtime" but serialized as "System.Private.CoreLib"
//		var assemblyNameMap = new Dictionary<string, string> {
//			{ "System.Private.CoreLib", "System.Runtime" },
//		};
//		if (assemblyNameMap.TryGetValue(assemblyName, out var mappedAssemblyName)) 
//			assemblyName = mappedAssemblyName;

			return $"clr-namespace:{type.Namespace};assembly={assemblyName}";
		}


		public static string? FindXmlnsPrefix(Type type) {
			if (type == null) throw new ArgumentNullException(nameof(type));

			var xmlNamespace = FindXmlnsDefinition(type);
			// [assembly: XmlnsPrefix(XmlNamespace, "utils")]
			var attr = type.Assembly.GetCustomAttributes<XmlnsPrefixAttribute>()
				.FirstOrDefault(a => a.XmlNamespace == xmlNamespace);

			return attr?.Prefix;
		}

		public static string GetResourceKeyString(object key, XmlNamespaceManager nsManager) {
			switch (key) {
				case string strKey:
					return strKey;
				case Type keyType:
					return $"{{x:Type {CreateQualifiedName(keyType, nsManager)}}}";
				case ComponentResourceKey componentResourceKey:
					var xamlType = CreateQualifiedName(componentResourceKey.TypeInTargetAssembly, nsManager);
					if (!xamlType.Contains(':'))
						return $"{{ComponentResourceKey {xamlType}, {componentResourceKey.ResourceId}}}";
					nsManager.TryAddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml", out var xPrefix);
					return $"{{ComponentResourceKey {{{xPrefix}:Type {xamlType}}}, {componentResourceKey.ResourceId}}}";
				// return $"{{ComponentResourceKey TypeInTargetAssembly={{{xPrefix}:Type {xamlType}, ResourceId={componentResourceKey.ResourceId}}}";
				// return $"{{ComponentResourceKey TypeInTargetAssembly={xamlType}, ResourceId={componentResourceKey.ResourceId}}}";
				default:
					return GetResourceKeyStringCompat(key);
			}
		}

		public static string GetResourceKeyStringCompat(object key, XmlNamespaceManager nsManager) {
			var dummyElement = new FrameworkElement();
			dummyElement.Resources.Add(key, new object());

			string xamlString;
			using (var stringWriter = new StringWriter()) {
				var xmlWriterSettings = new XmlWriterSettings {
					Indent = true,
					OmitXmlDeclaration = true,
					ConformanceLevel = ConformanceLevel.Fragment
				};

				using (var xmlWriter = XmlWriter.Create(stringWriter, xmlWriterSettings)) {
					foreach (string prefix in nsManager) {
						xmlWriter.WriteAttributeString("xmlns", prefix, null, nsManager.LookupNamespace(prefix));
					}
					XamlWriter.Save(dummyElement.Resources, xmlWriter);
				}
				xamlString = stringWriter.ToString();
			}
			var startIdx = xamlString.IndexOf("x:Key=\"") + "x:Key=\"".Length;
			var endIdx = xamlString.IndexOf("\"", startIdx);
			var keyString = xamlString.Substring(startIdx, endIdx - startIdx);

			return keyString;
		}

		public static string GetResourceKeyStringCompat(object key) {
			var dummyElement = new FrameworkElement();
			dummyElement.Resources.Add(key, new object());

			string xamlString;
			using (var stringWriter = new StringWriter())
			using (var xmlWriter = XmlWriter.Create(stringWriter)) {
				XamlWriter.Save(dummyElement.Resources, xmlWriter);
				xamlString = stringWriter.ToString();
			}

			// Suche den x:Key-Wert im serialisierten XAML
			var startIdx = xamlString.IndexOf("x:Key=\"") + "x:Key=\"".Length;
			var endIdx = xamlString.IndexOf("\"", startIdx);
			var keyString = xamlString.Substring(startIdx, endIdx - startIdx);

			return keyString;
		}

		/// <summary>
		/// Gets the CLR type represented by the specified XElement.
		/// </summary>
		/// <param name="element">The XElement representing the XAML element.</param>
		/// <param name="throwException">Indicates whether to throw an exception if the type cannot be resolved. Default is false.</param>
		/// <returns>The <see cref="Type"/> represented by the XElement, or null if the type cannot be resolved and <paramref name="throwException"/> is false.</returns>
		/// <exception cref="ArgumentException">Thrown when the type cannot be resolved and <paramref name="throwException"/> is true.</exception>
		/// <remarks>
		/// This method attempts to resolve the CLR type from the XAML element name. For example:
		/// <list type="bullet">
		/// <item>
		/// <description>&lt;Button&gt; returns typeof(Button)</description>
		/// </item>
		/// <item>
		/// <description>&lt;Button.Background&gt; returns typeof(Brush)</description>
		/// </item>
		/// </list>
		/// </remarks>
		[SuppressMessage("ReSharper", "FlagArgument")]
		public static Type? GetRepresentingType(XElement element, bool throwException=false) {
			// a) <Button> => typeof(Button)
			// b) <Button.Background> => typeof(Brush)
			var nameFragments = element.Name.LocalName.Split('.');
			var typeName = nameFragments.Length == 1 ? element.Name.LocalName : nameFragments[0];
			var namespaceName = element.Name.NamespaceName;
			var type = GetType(typeName, namespaceName);
			if (type == null && !throwException) return null;
			if (type == null) throw new ArgumentException("No matching type found.");
			if (nameFragments.Length == 1) return type;

			var prop = (MemberInfo?)type.GetProperty(nameFragments[1]) ?? 
			           (MemberInfo?)type.GetMethod("Get"+nameFragments[1], BindingFlags.Public|BindingFlags.Static| BindingFlags.FlattenHierarchy);
			if (prop == null && !throwException) return null;
			if (prop == null ) throw new ArgumentException("No matching property found.");
			return (prop as PropertyInfo)?.PropertyType ?? (prop as MethodInfo)?.ReturnType;
		}

		public static Type? GetType(string typeName, string namespaceName) {
			// Prüfen, ob es sich um einen CLR-Namespace handelt
			if (namespaceName.StartsWith("clr-namespace:")) {
				var parts = namespaceName.Split(';');
				var clrNamespace = parts[0].Substring("clr-namespace:".Length);
				var assemblyName = parts.Length > 1 && parts[1].StartsWith("assembly=")
					? parts[1].Substring("assembly=".Length)
					: null;
				if (new[] {"mscorlib", "System.Runtime"}.Contains(assemblyName)) assemblyName = null;
				var fullTypeName = $"{clrNamespace}.{typeName}";

				if (assemblyName == null) return Type.GetType(fullTypeName);
				return Type.GetType($"{fullTypeName}, {assemblyName}");
			}

			// Prüfen, ob es sich um einen URI-Namespace handelt, der mit XmlnsDefinition verknüpft ist
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				var xmlnsDefinitions = assembly.GetCustomAttributes<XmlnsDefinitionAttribute>();
				foreach (var xmlnsDefinition in xmlnsDefinitions)
					if (xmlnsDefinition.XmlNamespace == namespaceName) {
						var fullTypeName = $"{xmlnsDefinition.ClrNamespace}.{typeName}";
						var type = assembly.GetType(fullTypeName);
						if (type != null) return type;
					}
			}

			return null;
		}

		public static void SimplifyValues(XElement element) {
			foreach (var child in element.Elements().ToList()) {
				if (trySimplify(child, out var simplifiedValue)) {
					child.ReplaceWith(new XText(simplifiedValue));
				} else {
					SimplifyValues(child);
				}
			}
			return;

			bool trySimplify(XElement child, out string? value) {
				value = null;
				if (!isSimpleValue(child)) return false;
				if (isAttachedProperty(child)) return false;
				// Prüfen, ob es einen TypeConverter gibt, der den Wert des Elements in eine Zeichenfolge umwandeln kann
				var converter = TypeDescriptor.GetConverter(child.GetClrType());
				if (converter == null || !converter.CanConvertFrom(typeof(string))) return false;
				value = child.Value;
				return true;
			}

			bool isAttachedProperty(XElement child) {
				// special case: <Grid><Grid Grid.Column>
				var fragments = child.Name.LocalName.Split('.');
				if (fragments.Length == 1) return false;
				var objName = fragments[0];
				var type = GetType(objName, child.Name.NamespaceName);
				var fi = type.GetField($"{fragments[1]}Property",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy);
				return fi != null;
			}

			bool isSimpleValue(XElement child) {
				return !child.HasElements && !child.HasAttributes && !string.IsNullOrWhiteSpace(child.Value);
			}
		}

	

		public static void SimplifyValues_1(XElement element) {
			var simplifiableTypes = new HashSet<string> { "SolidColorBrush", "Color" };

			foreach (var child in element.Elements().ToList()) {
				if (trySimplify(child, out var simplifiedValue)) {
					child.ReplaceWith(new XText(simplifiedValue));
				} else {
					SimplifyValues_1(child);
				}
			}
			return;

			bool trySimplify(XElement child, out string? value) {
				value = null;
				if (!simplifiableTypes.Contains(child.Name.LocalName) || !isSimpleValue(child)) return false;
				value = child.Value;
				return true;
			}

			bool isSimpleValue(XElement child) {
				return !child.HasElements && !child.HasAttributes && !string.IsNullOrWhiteSpace(child.Value);
			}
		}

		public static void SimplifyElementToAttribute(XElement element) {
			foreach (var child in element.Elements().ToList()) {
				if (!child.HasElements && isPropertyElement(child)) {
					var attrXName = XName.Get(getName(child), getNamespace(child));
					element.SetAttributeValue(attrXName, child.Value);
					child.Remove();
				}
				else {
					SimplifyElementToAttribute(child);
				}
			}
			bool isPropertyElement(XElement childElement) 
				=> childElement.Name.LocalName.Contains('.');

			string getNamespace(XElement child) {
				return child.Name.Namespace.NamespaceName == element.Name.Namespace.NamespaceName
					? ""
					: child.Name.Namespace.NamespaceName;
			}
			string getName(XElement child) {
				var nameFragments = child.Name.LocalName.Split('.');
				return nameFragments[0] == element.Name.LocalName ? nameFragments[1] : child.Name.LocalName;
			}
		}

		public static XmlNamespaceManager CollectXmlNamespaces(object obj)
			=> CollectXmlNamespaces(XamlObjectToXDocument(obj));

		public static XmlNamespaceManager CollectXmlNamespaces(XDocument xDocument) {
			var namespaces = xDocument.Root.DescendantsAndSelf()
				.Attributes()
				.Where(a => a.IsNamespaceDeclaration)
				.GroupBy(a => a.Value)
				.Select(g => g.First())
				.ToDictionary(a => a.Name.LocalName == "xmlns" ? "" : a.Name.LocalName, a => a.Value);
			var nsManager = new XmlNamespaceManager(new NameTable());
			foreach (var entry in namespaces) {
				nsManager.AddNamespace(entry.Key,entry.Value);
			}
			return nsManager;
		}

		public static XDocument XamlObjectToXDocument(object xamlObject) {
			var settings = new XmlWriterSettings {
				OmitXmlDeclaration = true,
				Indent = true,
				CloseOutput = false
			};
			using var stream = new MemoryStream();
			using var xmlWriter = XmlWriter.Create(stream, settings);
			XamlWriter.Save(xamlObject, xmlWriter);
			stream.Position = 0;
			var doc = XDocument.Load(stream);
			return doc;
		}

		public static void WriteXamlObjectToStreamEx(object xamlObject, Stream outputStream) {
			var xamlSchemaContext = new XamlSchemaContext();
			var settings = new XmlWriterSettings {
				Indent = true, 
				OmitXmlDeclaration = true, 
				Encoding = Encoding.UTF8,
				CloseOutput = false
			};

			using var xmlWriter = XmlWriter.Create(outputStream, settings);
			var xamlWriterSettings = new XamlXmlWriterSettings();
			using var xamlWriter = new XamlXmlWriter(xmlWriter, xamlSchemaContext, xamlWriterSettings);
			using var objectReader = new XamlObjectReader(xamlObject, xamlSchemaContext);
			var nsManager = CreateXamlNamespaceManager();
			while (objectReader.Read()) {
				if (objectReader.Type?.UnderlyingType == typeof(TemplateContent)) {
					continue; // Oder spezielle Verarbeitung hier
				}
				if (objectReader.NodeType == XamlNodeType.NamespaceDeclaration) {
					var ns = (NamespaceDeclaration) objectReader.Value;
					if (ns != null) {
						var prefix = nsManager.LookupPrefix(ns.Namespace);
						if (prefix != null) {
							xamlWriter.WriteNamespace(new NamespaceDeclaration(ns.Namespace, prefix));
							continue;
						}
					}
				}
				xamlWriter.WriteNode(objectReader);
			}
			/*
			System.Xaml.XamlObjectReaderException: 'Beim Speichern des Inhalts aus verzögerten Ladevorgängen wurde eine Ausnahme ausgelöst.'
			NotSupportedException: Das Verzögern des TemplateContentLoader-Ladeprogramms unterstützt den Speichervorgang nicht.
			 */
		}

		private static XmlNamespaceManager CreateXamlNamespaceManager() {
			var nsManager = new XmlNamespaceManager(new NameTable());
			nsManager.InitXamlNamespace(addCommonNamespaces:true);
			return nsManager;
		}

		public static void Write<T>(object obj, Stream stream) {
			var tempDoc = XamlObjectToXDocument(obj);
			var nsManager = CollectXmlNamespaces(tempDoc);
			nsManager.RemoveNamespace("av","http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			nsManager.AddNamespace("","http://schemas.microsoft.com/winfx/2006/xaml/presentation");

			var settings = new XmlWriterSettings {
				OmitXmlDeclaration = true,
				Indent = true,
				CloseOutput = false,
			};
			using var xmlWriter = XmlWriter.Create(stream, settings);

			xmlWriter.WriteStartDocument();

			// Schreibe das UserControl-Element manuell mit allen gesammelten Namespaces und Prefixen
			xmlWriter.WriteStartElement(typeof(T).Name,nsManager.DefaultNamespace);
			foreach (var prefix in nsManager.Prefixes().Except(new[]{"xml","xmlns"})) {
				xmlWriter.WriteAttributeString("xmlns", prefix, null, nsManager.LookupNamespace(prefix));
			}

			// Schreibe die Attribute des UserControl (einschließlich x:Class, Width, Height, etc.)
			foreach (var attribute in tempDoc.Root.Attributes()) {
				if (attribute.IsNamespaceDeclaration) continue;
				xmlWriter.WriteAttributeString(attribute.Name.LocalName, attribute.Value);
			}

			// Schreibe den Inhalt des UserControls (alle untergeordneten Elemente)
			foreach (var node in tempDoc.Root.Elements()) {
				writeElementWithNamespace(node);
			}

			// Beende das UserControl-Element und das Dokument
			xmlWriter.WriteEndElement();
			xmlWriter.WriteEndDocument();
			xmlWriter.Flush();

			void writeElementWithNamespace(XElement element) {
				var prefix = nsManager.LookupPrefix(element.Name.NamespaceName);
				xmlWriter.WriteStartElement(prefix, element.Name.LocalName, element.Name.NamespaceName);

				// Schreibe die Attribute des Elements
				foreach (var attribute in element.Attributes()) {
					if (attribute.IsNamespaceDeclaration) continue;
					var attributePrefix = attribute.Name.NamespaceName == string.Empty
						? string.Empty
						: nsManager.LookupPrefix(attribute.Name.NamespaceName);
					xmlWriter.WriteAttributeString(attributePrefix, attribute.Name.LocalName, attribute.Name.NamespaceName, attribute.Value);
				}

				// Schreibe den Inhalt des Elements
				if (element.HasElements) {
					foreach (var child in element.Elements()) {
						writeElementWithNamespace(child);
					}
				} else {
					xmlWriter.WriteString(element.Value);
				}
				xmlWriter.WriteEndElement();
			}
		}

		public static XDocument ReadBaml(Uri uri) {
			var stream = Application.GetResourceStream(uri)?.Stream;
			if (stream == null) throw new FileNotFoundException("Resource not found.");
			return ReadBaml(stream);
		}

		public static XDocument ReadBaml(Stream stream) {
			var customServiceProvider = new InternalServiceProvider();
			using var reader = new Baml2006Reader(stream);
			using var outStream = new MemoryStream();
			var settings = new XamlXmlWriterSettings {
				AssumeValidInput = true,
				CloseOutput = false
			};
			using var writer = new XamlXmlWriter(outStream, reader.SchemaContext,settings);
			while (reader.Read()) {
				if (reader.NodeType == XamlNodeType.Value) {
					var value = reader.Value;
					switch (value) {
						case null: continue;
						case string s: writer.WriteValue(s); continue;
						case MarkupExtension mex:
							var convertedValue = mex.ProvideValue(null)?.ToString();
							writer.WriteValue(convertedValue);
							continue;
						case Stream stream1: {
//							var r = new StreamReader(stream1);
//							var s = r.ReadToEnd();
//							writer.WriteValue(s);
//							stream1.Position = 0;
//							var doc1 = ReadBaml(stream1);
//							var obj = XamlReader.Load(stream1);
//							var s = ReadBinaryStream(stream1);
//							writer.WriteValue(s);
							writer.WriteValue($"UNKNOWN {stream1.GetType().Name}");
							continue;
						}
						default:
							Console.WriteLine($"UNKNOWN: {value.GetType().Name}");
							writer.WriteValue(value.ToString());
							continue;
					}
				}
				else {
					writer.WriteNode(reader);
				}
			}
			outStream.Position = 0;
			var doc = XDocument.Load(outStream);
			if (doc.Root.Name.LocalName == "ResourceDictionary" && doc.Root.Attribute("DeferrableContent")?.Value !=null) {
				// <ResourceDictionary DeferrableContent="UNKNOWN MemoryStream" />
				stream.Position = 0;
				var obj = LoadObjectFromBaml<ResourceDictionary>(stream);
				var xaml = XamlWriter.Save(obj);
				doc =  XDocument.Parse(xaml);
			}
			return doc;
		}

		public static T LoadObjectFromBaml<T>(Stream bamlStream) {
			if (bamlStream == null) throw new ArgumentNullException(nameof(bamlStream));
			bamlStream.Position = 0;
			using var reader = new Baml2006Reader(bamlStream);
			using var writer = new XamlObjectWriter(reader.SchemaContext);
			while (reader.Read()) writer.WriteNode(reader);
			return (T)writer.Result;
		}

		public static string ReadBinaryStream(Stream stream) {
			stream.Position = 0;
			using var memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			var byteArray = memoryStream.ToArray();

			var hexString = new StringBuilder(byteArray.Length * 3);
			foreach (var b in byteArray) {
				hexString.AppendFormat("{0:X2} ", b);
			}

			return hexString.ToString();
		}

		public static void IdentifyAndReplaceRootElement(XDocument xDocument) {
			var root = xDocument.Root;
			var basicRootTypes = new[] {typeof(UserControl), typeof(Window), typeof(Page)};
			var rootType = root.GetClrType();
			var newRootType = basicRootTypes.FirstOrDefault(bt => rootType.IsAssignableTo(bt));
			if (newRootType == null) return;
			var newRootName = XName.Get(newRootType.Name, FindXmlnsDefinition(newRootType));
			root.Name = newRootName;
		}

		public static XDocument ReadBamlAndProcess(Uri uri) {
			var doc = ReadBaml(uri);
			IdentifyAndReplaceRootElement(doc);
			return doc;
		}

	} // class XamlUtils

	internal class InternalServiceProvider : IServiceProvider {

		public object? GetService(Type serviceType) {
			return null;
		}
	} 
} // namespace