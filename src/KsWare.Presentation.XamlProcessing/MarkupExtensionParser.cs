using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace KsWare.Presentation.XamlProcessing {

	public static class MarkupExtensionParser {

		public static T ParseMarkupExtension<T>(string componentKeyString, XmlNamespaceManager nsManager) where T : class,new() {
			var parsedValues = ParseMarkupExtension(componentKeyString);
			var type = typeof(T);
			T? instance = createInstance(parsedValues.Where(p => p.Key[0].IsAsciiDigit()).OrderBy(p => int.Parse(p.Key)).Select(p=>p.Value).ToArray());

			foreach (var kvp in parsedValues) {
				if (char.IsDigit(kvp.Key[0]) || kvp.Key[0]=='@') continue;
				setProperty(kvp.Key, kvp.Value);
			}
			return instance;

			T createInstance(IList<string> constructorParameter) {
//			switch (parsedValues["@Type"]) {
//				case "StaticExtension" when constructorParameter.Count == 1: {
//                    new StaticExtension()
//				} 
//			}
				var constructors = type.GetConstructors().Where(c=>c.GetParameters().Length==constructorParameter.Count); // Optional parameters are also counted
				foreach (var constructor in constructors) {
					var parameters = constructor.GetParameters();
					var args = new object[constructorParameter.Count];
					try {
						for (var i = 0; i < args.Length; i++) {
							args[i] = changeType(constructorParameter[i], parameters[i].ParameterType);
						}
						return (T)(Activator.CreateInstance(type, args) ?? throw new TypeInitializationException(type.FullName,null));
					}
					catch (Exception ex) { }
				}
				throw new InvalidOperationException("No matching constructor found for the given parameters.");
			}

			object changeType(string value, Type parameterType) {
				if (parameterType == typeof(object) || parameterType == typeof(string)) return value;
				var converter = TypeDescriptor.GetConverter(parameterType);
				if (converter != null && converter.CanConvertFrom(typeof(string))) 
					return converter.ConvertFromString(value);
				if (parameterType.IsAssignableTo(typeof(Type)))
					return XamlUtils.ResolveXamlType(value, nsManager);
				throw new InvalidOperationException($"Conversion from string to {parameterType} is not supported.");
			}

			void setProperty(string propertyName, string value) {
				var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
				if (property == null || !property.CanWrite) return;
				var convertedValue = changeType(value, property.PropertyType);
				property.SetValue(instance, convertedValue);
			}
		}

		public static Dictionary<string, string>ParseMarkupExtension(string markupExtension) {
			var dictionary = new Dictionary<string, string>();
			var trimmedString = markupExtension.Trim('{', '}');
			var firstSpaceIndex = trimmedString.IndexOf(' ');
			string typeName;
			string parameterString = null;

			if (firstSpaceIndex == -1) {
				typeName = trimmedString;
			} else {
				typeName = trimmedString.Substring(0, firstSpaceIndex);
				parameterString = trimmedString.Substring(firstSpaceIndex + 1).Trim();
			}
			dictionary.Add("@Type",typeName);
			if (!string.IsNullOrEmpty(parameterString)) {
				ParseParameters(parameterString, dictionary);
			}
			return dictionary;
		}

		private static void ParseParameters(string parameters, Dictionary<string, string> result) {
			int start = 0;
			bool inQuotes = false;
			bool inBraces = false;
			string currentKey = null;
			string value;
			for (int i = 0; i < parameters.Length; i++) {
				char c = parameters[i];

				if (c == '"' && !inBraces) inQuotes = !inQuotes;
				else if (c == '{') inBraces = true;
				else if (c == '}') inBraces = false;

				if (c == '=' && !inQuotes && !inBraces && currentKey == null) {
					currentKey = parameters.Substring(start, i - start).Trim();
					start = i + 1;
				} 
				else if (c == ',' && !inQuotes && !inBraces) {
					value = parameters.Substring(start, i - start).Trim();
					if (currentKey != null) {
						result.Add(currentKey, value);
						currentKey = null;
					} 
					else {
						result.Add($"{result.Count}", value); // first unnamed parameter starts with "1"
					}
					start = i + 1;
				}
			}
			if (start > parameters.Length) return;
			value = (parameters+" ").Substring(start).Trim(); if (string.IsNullOrWhiteSpace(value)) value = null;
			if (currentKey == null && value == null) return;
			if (value == null) ;//TODO ERROR Attribute value is expected
			result.Add(currentKey ?? $"{result.Count}", value);
		}
	}

}

