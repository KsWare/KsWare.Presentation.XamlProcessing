using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;
using NUnit.Framework;

namespace KsWare.Presentation.XamlProcessing.Tests {

	internal class MarkupExtensionParserTests {

		[TestCase(1,"{ComponentResourceKey TypeInTargetAssembly=MyNamespace.MyType, ResourceId=MyKey}", "@Type","ComponentResourceKey", "TypeInTargetAssembly", "MyNamespace.MyType", "ResourceId", "MyKey")]
		[TestCase(2,"{ComponentResourceKey MyNamespace.MyType, ResourceId=MyKey}", "@Type","ComponentResourceKey", "1", "MyNamespace.MyType", "ResourceId", "MyKey")]
		[TestCase(3,"{ComponentResourceKey TypeInTargetAssembly=MyNamespace.MyType, MyKey}", "@Type","ComponentResourceKey", "TypeInTargetAssembly", "MyNamespace.MyType", "2", "MyKey")]
		[TestCase(4,"{ComponentResourceKey MyNamespace.MyType, MyKey}", "@Type","ComponentResourceKey", "1", "MyNamespace.MyType", "2", "MyKey")]
		[TestCase(5,"{SomeOtherMarkup Type=MyType, Parameter=Value}", "@Type","SomeOtherMarkup", "Type", "MyType", "Parameter", "Value")]
		[TestCase(5,"{SomeOtherMarkup Type={x:Type Button}, Parameter=Value}", "@Type","SomeOtherMarkup", "Type", "{x:Type Button}", "Parameter", "Value")]
		[TestCase(6,"{ComponentResourceKey}","@Type", "ComponentResourceKey")]
		public void ParseMarkupExtension_ValidInputs_ReturnsExpectedParameters(int testNumber, string input, params string[] expectedKeyValuePairs) {
			// Act
			var result = MarkupExtensionParser.ParseMarkupExtension(input);

			// Assert each key-value pair
			for (var i = 0; i < expectedKeyValuePairs.Length; i += 2) {
				var expectedKey = expectedKeyValuePairs[i];
				var expectedValue = expectedKeyValuePairs[i + 1];

				Assert.That(result.ContainsKey(expectedKey), Is.True,
					$"The key '{expectedKey}' was not found in the result.");
				Assert.That(result[expectedKey], Is.EqualTo(expectedValue),
					$"The value for key '{expectedKey}' does not match the expected value.");
			}
		}

		[TestCase(1,"{InvalidMarkup", "@Type", "InvalidMarkup")]
		[TestCase(2,"{ComponentResourceKey MyNamespace.MyType, ResourceId=}", "@Type", "ComponentResourceKey", "1", "MyNamespace.MyType", "ResourceId", null)]
		public void ParseMarkupExtension_InvalidInputs_ReturnsPartialResults(int testNumber, string input, params string?[] expectedKeyValuePairs) {
			// Act
			var result = MarkupExtensionParser.ParseMarkupExtension(input);

			// Assert each key-value pair
			for (var i = 0; i < expectedKeyValuePairs.Length; i += 2) {
				var expectedKey = expectedKeyValuePairs[i];
				var expectedValue = expectedKeyValuePairs[i + 1];

				Assert.That(result.ContainsKey(expectedKey), Is.True,
					$"The key '{expectedKey}' was not found in the result.");
				Assert.That(result[expectedKey], Is.EqualTo(expectedValue),
					$"The value for key '{expectedKey}' does not match the expected value.");
			}
		}

		[TestCase("{ComponentResourceKey TypeInTargetAssembly=DataGrid, ResourceId=FocusBorderBrushKey}", typeof(DataGrid), "FocusBorderBrushKey")]
		[TestCase("{ComponentResourceKey DataGrid, FocusBorderBrushKey}", typeof(DataGrid), "FocusBorderBrushKey")]
		public void ParseMarkupExtension_ComponentResourceKey_ReturnsExpected(string input, Type expectedTypeInTargetAssembly, string expectedResourceId) {
			// 
			var nsManager = new XmlNamespaceManager(new NameTable());
			nsManager.AddNamespace("","http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			// Act
			var result = MarkupExtensionParser.ParseMarkupExtension<ComponentResourceKey>(input, nsManager);

			// Assert
			Assert.That(result.TypeInTargetAssembly, Is.EqualTo(expectedTypeInTargetAssembly));
			Assert.That(result.ResourceId.ToString(), Is.EqualTo(expectedResourceId));
		}

#if NETFRAMEWORK
		private const string PIString = "3.14159265358979";
#else
		private const string PIString = "3.141592653589793";
#endif

		[TestCase("{x:Static Colors.Black}", "Colors.Black", null,"#FF000000")]
		[TestCase("{x:Static Member=Colors.Black}", "Colors.Black", null,"#FF000000")]
		[TestCase("{x:Static Member=s:Math.PI}", "s:Math.PI", null, PIString)]
		[TestCase("{x:Static s:Math.PI}", "s:Math.PI", null, PIString)]
		public void ParseMarkupExtension_xStatic_ReturnsExpected(string input, string expectedMember, Type? expectedMemberType, object expectedResult) {
			var nsManager = new XamlNamespaceManager().InitCommonNamespaces();
			// Act
			var sut = MarkupExtensionParser.ParseMarkupExtension<StaticExtension>(input, nsManager);
			var result = sut.ProvideValue(new MockServiceProvider(nsManager));

			// Assert
			Assert.That(sut.Member, Is.EqualTo(expectedMember));
			Assert.That(sut.MemberType, Is.EqualTo(expectedMemberType));
			Assert.That(string.Format(CultureInfo.InvariantCulture,"{0}", result), Is.EqualTo(expectedResult));
		}

		[TestCase(1,"{x:Type Button}", "Button", typeof(Button), typeof(Button))]
		[TestCase(2,"{x:Type s:Int32}", "s:Int32", typeof(Int32), typeof(Int32))]
		[TestCase(3,"{x:Type Type=Button}", null, typeof(Button), typeof(Button))]
		[TestCase(4,"{x:Type TypeName=Button}", "Button", typeof(Button), typeof(Button))]
		public void ParseMarkupExtension_xType_ReturnsExpected(int testNumber, string input, string? expectedTypeName, Type? expectedType, Type expectedResult) {
			Console.WriteLine($"Test {testNumber}: {input}");
			var nsManager = new XmlNamespaceManager(new NameTable());
			nsManager.AddNamespace("","http://schemas.microsoft.com/winfx/2006/xaml/presentation");
			nsManager.AddNamespace("s","clr-namespace:System;assembly=System.Runtime");
			// Act
			var sut = MarkupExtensionParser.ParseMarkupExtension<System.Windows.Markup.TypeExtension>(input, nsManager);
			var result = sut.ProvideValue(new MockServiceProvider(nsManager));

			// Assert
			Assert.That(sut.TypeName, Is.EqualTo(expectedTypeName));
			Assert.That(sut.Type, Is.EqualTo(expectedType));
			Assert.That(result, Is.EqualTo(expectedResult));
		}

		class MockServiceProvider : IServiceProvider, IXamlTypeResolver {

			private readonly XmlNamespaceManager _nsManager;

			public MockServiceProvider(XmlNamespaceManager nsManager) {
				_nsManager = nsManager;
			}

			public object GetService(Type serviceType) {
				if (GetType().GetInterfaces().Contains(serviceType)) return this;
				throw new NotImplementedException($"Mock service '{serviceType.Name}' not implemented.");
			}

			Type IXamlTypeResolver.Resolve(string qualifiedTypeName) {
				return XamlUtils.ResolveXamlType(qualifiedTypeName, _nsManager);
			}
		}

	}

}