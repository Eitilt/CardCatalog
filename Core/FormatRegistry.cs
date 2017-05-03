/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using AgEitilt.Common.Dictionary.Extensions;

namespace AgEitilt.CardCatalog {
	/// <summary>
	/// A data-free class providing a common means to work with multiple
	/// metadata formats.
	/// </summary>
	public static class FormatRegistry {
		internal class FormatData : ReflectionData<MetadataTag> {
			public Dictionary<byte[], ReflectionData<TagField>> fields = new Dictionary<byte[], ReflectionData<TagField>>(FieldDictionary.KeyComparer);
		}

		/// <summary>
		/// The class encapsulating each registered metadata format.
		/// </summary>
		internal static Dictionary<string, FormatData> tagFormats = new Dictionary<string, FormatData>();

		/// <summary>
		/// Get all field types registered for the given metadata format.
		/// </summary>
		/// 
		/// <param name="format">The short name of the desired format.</param>
		/// 
		/// <returns>
		/// The classes implementing each <see cref="TagField"/>.
		/// </returns>
		public static IReadOnlyDictionary<byte[], Type> FieldTypes(string format) =>
			tagFormats[format].fields.ToDictionary(
				p => p.Key,
				p => p.Value.Type,
				FieldDictionary.KeyComparer
			);

		/// <summary>
		/// A registry of previously-scanned assemblies in order to prevent
		/// unnecessary use of reflection methods.
		/// </summary>
		static HashSet<string> assemblies = new HashSet<string>();

		/// <summary>
		/// Scan the assembly enclosing the specified class for
		/// implementations of metadata formats.
		/// </summary>
		/// 
		/// <remarks>
		/// The explicit type parameter is preferred over scanning loaded assemblies
		/// as oftentimes this will be the first time the 
		/// </remarks>
		/// 
		/// <typeparam name="T">
		/// Any type from the assembly to scan.
		/// </typeparam>
		public static void RegisterAll<T>() {
			RegisterAll(typeof(T).GetTypeInfo().Assembly);
		}

		/// <summary>
		/// Scan the given assembly for all types marked as implementing a
		/// metadata format in a suitable manner for automatic lookup.
		/// <para/>
		/// Subsequent calls on a previously-scanned assembly will be ignored
		/// in order to save unnecessary type reflection.
		/// </summary>
		/// 
		/// <param name="assembly">The assembly to scan.</param>
		/// 
		/// <seealso cref="MetadataFormatAttribute"/>
		public static void RegisterAll(Assembly assembly) {
			// Avoid searching assemblies multiple times to cut down on the
			// performance hit of reflection
			if (assemblies.Contains(assembly.FullName))
				return;

			foreach (Type t in assembly.ExportedTypes) {
				// Provide cache for if it becomes necessary
				MethodInfo tagRegisterFunction = null;
				MethodInfo fieldRegisterFunction = null;

				// Register every type as it is described by its attributes
				foreach (var tagAttr in t.GetTypeInfo().GetCustomAttributes<MetadataFormatAttribute>(false)) {
					if (tagRegisterFunction == null)
						tagRegisterFunction = tagRegisterGeneric.MakeGenericMethod(new Type[1] { t });

					tagRegisterFunction.Invoke(null, new object[1] { tagAttr.Name });
				}
				foreach (var fieldAttr in t.GetTypeInfo().GetCustomAttributes<TagFieldAttribute>(false)) {
					if (fieldRegisterFunction == null)
						fieldRegisterFunction = fieldRegisterGeneric.MakeGenericMethod(new Type[1] { t });

					fieldRegisterFunction.Invoke(null, new object[2] { fieldAttr.Format, fieldAttr.Header });
				}
			}

			assemblies.Add(assembly.FullName);
		}
		/// <summary>
		/// Cache the method used to register new tag formats in
		/// <see cref="RegisterAll(Assembly)"/>.
		/// </summary>
		/// 
		/// <remarks>
		/// This needs to be invoked via reflection since we determine the
		/// generic type via a <see cref="Type"/> variable.
		/// </remarks>
		static readonly MethodInfo tagRegisterGeneric = typeof(FormatRegistry).GetMethod(nameof(Register), new Type[1] { typeof(string) });
		/// <summary>
		/// Cache the method used to register new field formats in
		/// <see cref="RegisterAll(Assembly)"/>.
		/// </summary>
		/// 
		/// <remarks>
		/// This needs to be invoked via reflection since we determine the
		/// generic type via a <see cref="Type"/> variable.
		/// </remarks>
		static readonly MethodInfo fieldRegisterGeneric = typeof(FormatRegistry).GetMethod(nameof(Register), new Type[2] { typeof(string), typeof(byte[]) });

		/// <summary>
		/// Add the single given metadata format type to the lookup tables
		/// according to the descriptor specified in each of its
		/// <see cref="MetadataFormatAttribute.Name"/> fields.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that if multiple types are registered under the same name,
		/// any later registrations will override the previous.
		/// </remarks>
		/// 
		/// <typeparam name="T">
		/// The <see cref="MetadataTag"/> class implementing the format.
		/// </typeparam>
		/// 
		/// <exception cref="TypeLoadException">
		/// <typeparamref name="T"/> does not have any associated
		/// <see cref="MetadataFormatAttribute"/>.
		/// </exception>
		/// 
		/// <seealso cref="HeaderParserAttribute"/>
		public static void Register<T>() where T : MetadataTag {
			var attrs = typeof(T).GetTypeInfo().GetCustomAttributes<MetadataFormatAttribute>(false);
			if (attrs.Count() == 0)
				throw new TypeLoadException(Strings.Base.Exception_NoFormatName);

			foreach (MetadataFormatAttribute attr in attrs)
				Register<T>(attr.Name);
		}

		/// <summary>
		/// Add the given metadata format type to the lookup tables under the
		/// specified custom descriptor, even if it does not have any
		/// associated <see cref="MetadataFormatAttribute"/>.
		/// </summary>
		/// 
		/// <remarks>
		/// The validation function must still be identified with a
		/// <see cref="HeaderParserAttribute"/>.
		/// <para/>
		/// Note that if multiple types are registered under the same name,
		/// any later registrations will override the previous.
		/// </remarks>
		/// 
		/// <typeparam name="T">
		/// The <see cref="MetadataTag"/> class implementing the format.
		/// </typeparam>
		/// 
		/// <param name="format">
		/// A short name for the format to be used as an access key for later
		/// lookups.
		/// <para/>
		/// It is recommended that this also be exposed as a constant.
		/// </param>
		public static void Register<T>(string format) where T : MetadataTag {
			var formatType = typeof(T);

			tagFormats.GetOrAdd(format).Type = formatType;

			// Register the type once for each potential header layout
			//TODO: Stop multiple header parsers from overwriting each other
			foreach (var m in from method in formatType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
							  where method.IsDefined(typeof(HeaderParserAttribute))
							  select method)
				Register(format, m.GetCustomAttribute<HeaderParserAttribute>(true).HeaderLength, m);
		}

		/// <summary>
		/// Add the given tag field type to the lookup tables under the proper
		/// format specifier.
		/// </summary>
		/// 
		/// <typeparam name="T">
		/// The <see cref="TagField"/> class implementing the field.
		/// </typeparam>
		/// 
		/// <param name="format">
		/// The short name of the format defining this field, or `null` to
		/// autodetect from the enclosing class.
		/// </param>
		/// <param name="header">
		/// The unique specifier of this field.
		/// </param>
		public static void Register<T>(string format, byte[] header) where T : TagField {
			Register<T>(format, new byte[1][] { header });
		}

		/// <summary>
		/// Add the given tag field type to the lookup tables under the proper
		/// format specifier.
		/// </summary>
		/// 
		/// <typeparam name="T">
		/// The <see cref="TagField"/> class implementing the field.
		/// </typeparam>
		/// 
		/// <param name="format">
		/// The short name of the format defining this field, or `null` to
		/// autodetect from the enclosing class.
		/// </param>
		/// <param name="headerList">
		/// The unique specifiers of this field.
		/// </param>
		public static void Register<T>(string format, IEnumerable<byte[]> headerList) where T : TagField {
			var fieldType = typeof(T);

			IEnumerable<string> formatAttrs = Array.Empty<string>();
			// Detect associated format from context
			if (format == null) {
				// Find the innermost enclosing type with a format attribute
				for (Type enclosingType = fieldType.DeclaringType;
						(formatAttrs.Count() == 0) && (enclosingType != null);
						enclosingType = enclosingType.DeclaringType) {
					// Extract the format names from each such attribute
					formatAttrs =
						from attr in enclosingType.GetTypeInfo().GetCustomAttributes<MetadataFormatAttribute>(false)
						select attr.Name;
				}

				// Reached a top-level type without finding a format attribute
				if (formatAttrs.Count() == 0)
					throw new MissingFieldException(Strings.Base.Exception_NoFieldEnclosingFormatName);
			// Use the format passed at method invocation
			} else {
				formatAttrs = new string[1] { format };
			}

			var headers = new List<byte[]>();
			// Detect applicable field headers from contained generators
			if ((headerList == null) || (headerList.Contains(null))) {
				headers.AddRange(from field in fieldType.GetFields(BindingFlags.Static | BindingFlags.Public)
								 where field.IsDefined(typeof(FieldNamesAttribute))
								 from name in field.GetValue(null) as IEnumerable<byte[]>
								 select name);
				headers.AddRange(from property in fieldType.GetProperties(BindingFlags.Static | BindingFlags.Public)
								 where property.IsDefined(typeof(FieldNamesAttribute))
								 from name in property.GetValue(null) as IEnumerable<byte[]>
								 select name);
				headers.AddRange(from method in fieldType.GetMethods(BindingFlags.Static | BindingFlags.Public)
								 where method.IsDefined(typeof(FieldNamesAttribute))
								 from name in (method.CreateDelegate(typeof(Func<IEnumerable<byte[]>>)) as Func<IEnumerable<byte[]>>).Invoke()
								 select name);
			// Use the headers passed at method invocation
			} else if (headerList != null) {
				headers.AddRange(from header in headerList
								 where header != null
								 select header);
			}

			// Add all fields for all formats
			foreach (var name in formatAttrs) {
				foreach (var head in headers) {
					var field = tagFormats.GetOrAdd(name).fields.GetOrAdd(head);
					field.Type = fieldType;

					foreach (var m in from method in fieldType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
									  where method.IsDefined(typeof(HeaderParserAttribute))
									  select Tuple.Create(method.GetCustomAttribute<HeaderParserAttribute>(false).HeaderLength, method))
						Register(name, head, m.Item1, m.Item2);
				}
			}
		}

		/// <summary>
		/// Group the restriction checking of validation functions for easy
		/// reuse.
		/// </summary>
		/// 
		/// <typeparam name="T">
		/// The required return type of <paramref name="method"/>.
		/// </typeparam>
		/// 
		/// <param name="method">The validation function to check.</param>
		/// 
		/// <exception cref="NotSupportedException">
		/// The method fails some requirement for it to be a functional header
		/// validation function.
		/// </exception>
		private static void MethodSanityChecks<T>(MethodInfo method) {
			if (method.IsPrivate)
				throw new NotSupportedException(Strings.Base.Exception_ParseFunctionPrivate);
			if (method.IsAbstract)
				throw new NotSupportedException(Strings.Base.Exception_ParseFunctionAbstract);
			if (method.IsStatic == false)
				throw new NotSupportedException(Strings.Base.Exception_ParseFunctionNonstatic);

			var parameters = method.GetParameters();
			if ((parameters.Length == 0)
				|| (typeof(IEnumerable<byte>).IsAssignableFrom(parameters[0].ParameterType) == false)
				|| ((parameters.Length > 1) && (parameters[1].IsOptional == false)))
				throw new NotSupportedException(Strings.Base.Exception_ParseFunctionParameters);

			if (typeof(T).IsAssignableFrom(method.ReturnType) == false)
				throw new NotSupportedException(Strings.Base.Exception_ParseFunctionReturn);
		}

		/// <summary>
		/// Add the given format initialization method to the lookup tables
		/// under the specified custom descriptor.
		/// </summary>
		/// 
		/// <remarks>
		/// This should almost purely be called via
		/// <see cref="Register{T}(string)"/>, and has been separated
		/// primarily for code clarity.
		/// </remarks>
		/// 
		/// <param name="format">
		/// A short name for the format to be used as an access key for later
		/// lookups.
		/// </param>
		/// <param name="headerLength">
		/// The number of bytes <paramref name="method"/> uses to read the tag
		/// header.
		/// </param>
		/// <param name="method">The method to add.</param>
		private static void Register(string format, uint headerLength, MethodInfo method) {
			MethodSanityChecks<MetadataTag>(method);

			try {
				var validation = new HeaderValidation<MetadataTag>() {
					length = (int)headerLength,
					function = method.CreateDelegate(typeof(HeaderValidation<MetadataTag>.Validator))
							as HeaderValidation<MetadataTag>.Validator
				};
				var functions = tagFormats.GetOrAdd(format).ValidationFunctions;
				functions.Add(validation);
			} catch (ArgumentException e) {
				throw new NotSupportedException(e.Message, e.InnerException);
			}
		}

		/// <summary>
		/// Add the given field initialization method to the lookup tables
		/// under the specified custom descriptor.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that if multiple methods are registered under the same
		/// <paramref name="format"/> and <paramref name="field"/> pairing,
		/// any later registrations will override the previous.
		/// <para/>
		/// This should almost purely be called via
		/// <see cref="Register{T}(string)"/>, and has been separated
		/// primarily for code clarity.
		/// </remarks>
		/// 
		/// <param name="format">
		/// A short name for the format to be used as an access key for later
		/// lookups.
		/// </param>
		/// <param name="field">
		/// The unique identifier for the associated field.
		/// </param>
		/// <param name="headerLength">
		/// The number of bytes <paramref name="method"/> uses to read the tag
		/// header.
		/// </param>
		/// <param name="method">The method to add.</param>
		private static void Register(string format, byte[] field, uint headerLength, MethodInfo method) {
			MethodSanityChecks<TagField>(method);

			try {
				var validation = new HeaderValidation<TagField>() {
					length = (int)headerLength,
					function = method.CreateDelegate(typeof(HeaderValidation<TagField>.Validator))
							as HeaderValidation<TagField>.Validator
				};
				var functions = tagFormats.GetOrAdd(format).fields.GetOrAdd(field).ValidationFunctions;
				functions.Add(validation);
			} catch (ArgumentException e) {
				throw new NotSupportedException(e.Message, e.InnerException);
			}
		}

		/// <summary>
		/// Parse all recognized tags in a stream asynchronously.
		/// </summary>
		/// 
		/// <param name="stream">The stream to parse.</param>
		/// 
		/// <returns>The resulting <see cref="MetadataTag"/>s.</returns>
		public async static Task<IEnumerable<MetadataTag>> ParseAsync(Stream stream) {
			return await ReflectionData<MetadataTag>.ParseAsync(stream, tagFormats.Values);
		}
	}
}
