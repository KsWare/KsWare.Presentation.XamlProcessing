using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using XamlWriter = System.Windows.Markup.XamlWriter;

namespace KsWare.Presentation.XamlProcessing.Tests {

	public class XamlUtilsTests {

		[TestCase("Button", typeof(Button))]
		[TestCase("s:Int32", typeof(int))]
		[TestCase("{x:Type Button}", typeof(Button))]
		[TestCase("{x:Type s:String}", typeof(string))]
		public void ResolveXamlType_ValidTypes_ReturnsExpectedType(string input, Type expectedType) {
			// Prepare
			var nsManager = new XmlNamespaceManager(new NameTable());
			nsManager.AddNamespace("","http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			nsManager.AddNamespace("s","clr-namespace:System;assembly=System.Runtime");
			// Act
			var result = XamlUtils.ResolveXamlType(input, nsManager);
			// Assert
			Assert.That(result, Is.EqualTo(expectedType));
		}

		[TestCase("NonExistentType")]
		public void ResolveXamlType_InvalidType_ThrowsTypeLoadException(string input) {
			// Prepare
			var nsManager = new XmlNamespaceManager(new NameTable());
			// Assert
			Assert.That(() => XamlUtils.ResolveXamlType(input, nsManager), Throws.TypeOf<TypeLoadException>());
		}
	
		[TestCase(typeof(DataGrid),"FocusBorderBrushKey","{ComponentResourceKey DataGrid, FocusBorderBrushKey}")]
		[TestCase(typeof(SystemColors),"HighlightBrushKey","{x:Static SystemColors.HighlightBrushKey}")]
		[Apartment(ApartmentState.STA)]
		public void GetResourceKeyStringTest(Type type, object keyProperty, string expected) {
			var nsManager = new XmlNamespaceManager(new NameTable());nsManager.InitXamlNamespace(addCommonNamespaces:true);

			var key = type.GetProperty($"{keyProperty}", BindingFlags.Static|BindingFlags.Public)?.GetValue(null);
			// Act
			var keyString = XamlUtils.GetResourceKeyString(key,nsManager);
			//Assert
			Assert.That(keyString,Is.EqualTo(expected));
		}

		[TestCase(typeof(Button),"http://schemas.microsoft.com/winfx/2006/xaml/presentation")]
#if NET
	[TestCase(typeof(Int32),"clr-namespace:System;assembly=System.Private.CoreLib")]
#elif NETFRAMEWORK
		[TestCase(typeof(Int32),"clr-namespace:System;assembly=mscorlib")] // ???
#endif
		public void GetXmlNamespaceTest(Type type, string result) {
			Assert.That(XamlUtils.FindXmlnsDefinition(type),Is.EqualTo(result));
		}

		private XDocument CreateXDocument() {
			var stream = new MemoryStream();
			var dic = new ResourceDictionary {Source = new Uri("pack://application:,,,/KsWare.Presentation.XamlProcessing.Tests;component/Resources/GetXamlTypeNameTest.xaml")};
			var writer = new XmlTextWriter(stream, System.Text.Encoding.UTF8);
			writer.Formatting = Formatting.Indented;
			XamlWriter.Save(dic, writer);
			writer.Flush();
			stream.Position = 0;
			var xDocument = XDocument.Load(stream);
			Debug.WriteLine($"{xDocument}");
			return xDocument;
		}

		[TestCase(1, typeof(Button), "Button", false)]
		[TestCase(2, typeof(Int32), "s:Int32", true)]
		public void CreateQualifiedNameTest(int testNumber, Type type, string expectedName, bool isNew) {
			var nsManager = new XmlNamespaceManager(new NameTable());
			nsManager.AddNamespace("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			var x = nsManager.GetLastAddedPrefix();
			var qName = XamlUtils.CreateQualifiedName(type, nsManager);
			Assert.That(qName, Is.EqualTo(expectedName));
			Assert.That(x != nsManager.GetLastAddedPrefix(), Is.EqualTo(isNew));

		}

		[TestCase(1, typeof(Int32), "s1:Int32", true)]
		public void CreateQualifiedName_incrementsuffix(int testNumber, Type type, string expectedName, bool isNew) {
			var nsManager = new XmlNamespaceManager(new NameTable());
			nsManager.AddNamespace("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			nsManager.AddNamespace("s", "reserved");
			var x = nsManager.GetLastAddedPrefix();
			var qName = XamlUtils.CreateQualifiedName(type, nsManager);
			Assert.That(qName, Is.EqualTo(expectedName));
			Assert.That(x != nsManager.GetLastAddedPrefix(), Is.EqualTo(isNew));
		}

		[TestCase("<Button>", typeof(Button))]
		[TestCase("<s:Int32>", typeof(Int32))]
		[TestCase("<Button.Foreground>", typeof(Brush))]
		public void GetRepresentingType_WithValidElement_ReturnsType(string elementInput, Type expectedType) {
			var element = CreateXElement(elementInput);
			Assert.That(XamlUtils.GetRepresentingType(element), Is.EqualTo(expectedType));
		}

		[TestCase("<UnknownElement>")]
		public void GetRepresentingType_WithInvalidElement_ThrowsExceptionWhenFlagIsTrue(string elementInput) {
			var element = CreateXElement(elementInput);
			Assert.That(()=>XamlUtils.GetRepresentingType(element,true), Throws.ArgumentException);
		}	

		[TestCase("<UnknownElement>")]
		public void GetRepresentingType_WithInvalidElement_ReturnsNullWhenFlagIsFalse(string elementInput) {
			var element = CreateXElement(elementInput);
			Assert.That(XamlUtils.GetRepresentingType(element,false), Is.Null);
		}	

		private XElement CreateXElement(string elementInput) {
			if (elementInput.StartsWith("<s:")) {
				var localName = elementInput.Substring(3, elementInput.Length - 4); // Extrahiere "Int32" aus "<s:Int32>"
				#if NETFRAMEWORK 
				return new XElement(XName.Get(localName, "clr-namespace:System;assembly=mscorlib"));
				#else
				return new XElement(XName.Get(localName, "clr-namespace:System;assembly=System.Runtime"));
				#endif
			} else {
				var localName = elementInput.Substring(1, elementInput.Length - 2); // Extrahiere "Button" aus "<Button>"
				return new XElement(XName.Get(localName, "http://schemas.microsoft.com/winfx/2006/xaml/presentation"));
			}
		}

		[Apartment(ApartmentState.STA)]
		private object LoadPage(Uri uri) {
			//uri = new System.Uri("/KsWare.Presentation.XamlProcessing.Tests;component/Data/SimplifyElementsTest.xaml", System.UriKind.Relative);
			var xamlObject = Application.LoadComponent(uri);
			return xamlObject;

//		var stream = Application.GetResourceStream(uri)?.Stream;
//		if (stream == null) Assert.Fail("Resource not found: " + uri);
//		using var reader = new Baml2006Reader(stream);
//		var xamlObject = XamlReader.Load(reader);

//		var xamlString = XamlWriter.Save(xamlObject);

		}

		private object LoadPage(string uri) {
			var uriObj = new Uri(uri, UriKind.Relative);
			return LoadPage(uriObj);
		}

		public static XDocument LoadLocalFileAsXDocument(Uri uri) {
			var relativePath = uri.ToString().Split(new[] { "component/" }, StringSplitOptions.None)[1];
			var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var fullPath = Path.Combine(assemblyLocation, relativePath.Replace('/', Path.DirectorySeparatorChar));
			if (!File.Exists(fullPath)) throw new FileNotFoundException($"Die Datei {fullPath} wurde nicht gefunden.");
			var xamlDocument = XDocument.Load(fullPath);
			return xamlDocument;
		}

		[Test]
		[Apartment(ApartmentState.STA)]
		public void SimplifyElementToAttributeTest() {
			var uri = new Uri("/KsWare.Presentation.XamlProcessing.Tests;component/Data/SimplifyElementsTest.xaml", System.UriKind.Relative);
			var xDoc = LoadLocalFileAsXDocument(uri);
			// Act
			XamlUtils.SimplifyElementToAttribute(xDoc.Root);
			Console.WriteLine(xDoc.ToString());
		}

		[Test]
		[Apartment(ApartmentState.STA)]
		public void SimplifyValueTest() {
			var uri = new Uri("/KsWare.Presentation.XamlProcessing.Tests;component/Data/SimplifyElementsTest.xaml", System.UriKind.Relative);
			var xDoc = LoadLocalFileAsXDocument(uri);
			// Act
			XamlUtils.SimplifyValues(xDoc.Root);
			Console.WriteLine(xDoc.ToString());
		}

		[Test]
		[Apartment(ApartmentState.STA)]
		public void WriteXamlObjectToStreamExTest() {
			var obj = LoadPage("/KsWare.Presentation.XamlProcessing.Tests;component/Data/GetXamlTypeNameTest.xaml");
			using var stream = new MemoryStream();
			XamlUtils.WriteXamlObjectToStreamEx(obj, stream);
			stream.Position = 0;
			using var r = new StreamReader(stream);
			Console.WriteLine(r.ReadToEnd());
		}

		[Test]
		[Apartment(ApartmentState.STA)]
		public void XamlWriterSaveTest() {
			var obj = LoadPage("/KsWare.Presentation.XamlProcessing.Tests;component/Data/SimplifyElementsTest.xaml");
			var xaml = XamlWriter.Save(obj);
			Console.WriteLine(xaml);
		}

		[Test]
		[Apartment(ApartmentState.STA)]
		public void WriteUserControl() {
			var obj = LoadPage("/KsWare.Presentation.XamlProcessing.Tests;component/Data/SimplifyElementsTest.xaml");
			using var stream = new MemoryStream();
			// Act
			XamlUtils.Write<UserControl>(obj,stream);

			Console.WriteLine(StreamToString(stream));
		}

		[TestCase("Data/SimplifyElementsTest.xaml")]
		public void ReadBaml_FromResource_ReturnsValidXDocument(string file) {
			var uri = new Uri($"/KsWare.Presentation.XamlProcessing.Tests;component/{file}", UriKind.Relative);
			var doc=XamlUtils.ReadBaml(uri);
			Console.WriteLine(doc.ToString());
		}

		[TestCase("Data/MarkupExtensionParserTests.xaml")]
		[TestCase("Data/SimplifyElementsTest.xaml")]
		[Apartment(ApartmentState.STA)]
		public void ReadBamlAndProcess_FromResource_ReturnsValidXDocument(string file) {
			var uri = new Uri($"/KsWare.Presentation.XamlProcessing.Tests;component/{file}", UriKind.Relative);
			var doc=XamlUtils.ReadBamlAndProcess(uri);
			Console.WriteLine(doc.ToString());
		}


		private static string StreamToString(Stream stream) {
			var p = stream.Position;
			stream.Position = 0;
			using var r = new StreamReader(stream, Encoding.UTF8, true, 1024, true);
			var s = r.ReadToEnd();
			stream.Position = p;
			return s;
		}

	}

}
