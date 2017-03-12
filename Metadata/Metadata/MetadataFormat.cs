using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Metadata {
	/// <summary>
	/// A data-free class providing a common means to work with multiple
	/// metadata formats.
	/// </summary>
	public static class MetadataFormat {
		internal class FormatData : ReflectionData<MetadataTag> {
			/// <summary>
			/// Maintain a single instance of the comparer rather than
			/// creating a new one for each dictionary.
			/// </summary>
			/// 
			/// <remarks>
			/// TODO: Extract from <see cref="FieldDictionary"/>.
			/// </remarks>
			static Helpers.SequenceEqualityComparer<byte> fieldKeyComparer = new Helpers.SequenceEqualityComparer<byte>();

			public Dictionary<byte[], ReflectionData<TagField>> fields = new Dictionary<byte[], ReflectionData<TagField>>(fieldKeyComparer);
		}

		/// <summary>
		/// The class encapsulating each registered metadata format.
		/// </summary>
		private static Dictionary<string, FormatData> tagFormats = new Dictionary<string, FormatData>();

		internal static IEnumerable<ReflectionData<TagField>> FormatFields(string format) =>
			tagFormats[format].fields.Values;

		/// <summary>
		/// A registry of previously-scanned assemblies in order to prevent
		/// unnecessary use of reflection methods.
		/// </summary>
		static HashSet<string> assemblies = new HashSet<string>();

		/// <summary>
		/// Automatically add any metadata classes in opened assemblies on
		/// first call to the class.
		/// </summary>
		/// 
		/// <seealso cref="RefreshFormats"/>
		static MetadataFormat() {
			RefreshFormats<MetadataTag>();
		}

		/// <summary>
		/// Scan the assembly enclosing the specified for implementations of
		/// metadata formats.
		/// </summary>
		/// 
		/// <remarks>
		/// The explicit type parameter is preferred over scanning loaded assemblies
		/// as oftentimes this will be the first time the 
		/// </remarks>
		/// 
		/// <typeparam name="T">
		/// A type from the assembly to scan, extending <see cref="MetadataTag"/>.
		/// </typeparam>
		/*TODO: Could be nice to scan all referenced assemblies (loaded or
		 * not) and load any with the attribute that aren't yet, to avoid
		 * needing to refresh manually. See .NET Core's AssemblyLoadContext.
		 */
		public static void RefreshFormats<T>() where T : MetadataTag {
			Register(typeof(T).GetTypeInfo().Assembly);
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
		public static void Register(Assembly assembly) {
			// Avoid searching assemblies multiple times to cut down on the
			// performance hit of reflection
			if (assemblies.Contains(assembly.FullName))
				return;

			foreach (Type t in assembly.ExportedTypes) {
				var tagAttr = t.GetTypeInfo().GetCustomAttribute<MetadataFormatAttribute>(false);
				if (tagAttr != null)
					typeof(MetadataFormat).GetMethod("Register", Array.Empty<Type>())
						.MakeGenericMethod(new Type[1] { t })
						.Invoke(null, null);

				var fieldAttr = t.GetTypeInfo().GetCustomAttribute<TagFieldAttribute>(false);
				if (fieldAttr != null)
					Register(fieldAttr.Format, fieldAttr.Header, t);
			}

			assemblies.Add(assembly.FullName);
		}
		
		/// <summary>
		/// Add the given metadata format type to the lookup tables according
		/// to the  descriptor specified in its
		/// <see cref="MetadataFormatAttribute.Name"/>.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that if multiple types are registered under the same name,
		/// any later registrations will override the previous.
		/// </remarks>
		/// 
		/// <typeparam name="T">The type to add.</typeparam>
		/// 
		/// <seealso cref="HeaderParserAttribute"/>
		public static void Register<T>() where T : MetadataTag {
			var attr = typeof(T).GetTypeInfo().GetCustomAttribute<MetadataFormatAttribute>(false)
				?? throw new TypeLoadException("No explicit format name was passed, and the type has no attribute to infer it from");

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
		/// <param name="format">
		/// A short name for the format to be used as an access key for later
		/// lookups.
		/// <para/>
		/// It is recommended that this also be exposed as a constant.
		/// </param>
		/// 
		/// <typeparam name="T">The type to register.</typeparam>
		public static void Register<T>(string format) where T : MetadataTag {
			var formatType = typeof(T);

			tagFormats.GetOrCreate(format).type = formatType;

			foreach (var method in formatType.GetRuntimeMethods().Where(m => m.IsDefined(typeof(HeaderParserAttribute))))
				Register(format, method.GetCustomAttribute<HeaderParserAttribute>().HeaderLength, method);
		}

		/// <summary>
		/// Add the given tag field type to the lookup tables under the proper
		/// format specifier.
		/// </summary>
		/// 
		/// <param name="format">
		/// The short name of the format defining this field, or `null` to
		/// autodetect from the enclosing class.
		/// </param>
		/// <param name="header">
		/// The unique specifier of this field.
		/// </param>
		/// <param name="fieldType">
		/// The <see cref="Type"/> implementing the field.
		/// </param>
		private static void Register(string format, byte[] header, Type fieldType) {
			if (typeof(TagField).IsAssignableFrom(fieldType) == false)
				throw new NotSupportedException("Metadata tag field types must extend TagField");

			IEnumerable<string> formatAttrs;
			if (format == null) {
				formatAttrs =
					from attr in fieldType.DeclaringType.GetTypeInfo().GetCustomAttributes(typeof(MetadataFormatAttribute), true)
					select (attr as MetadataFormatAttribute).Name;

				if (formatAttrs == null)
					throw new MissingFieldException("If a TagFieldAttribute does not declare a Format,"
						+ " at least one enclosing type must have an attached MetadataFormatAttribute");

			} else {
				formatAttrs = new string[1] { format };
			}

			foreach (var name in formatAttrs) {
				var field = tagFormats.GetOrCreate(name).fields.GetOrCreate(header);
				field.type = fieldType;
			}
		}
		
		/// <summary>
		/// Add the given method to the lookup tables under the specified
		/// custom descriptor.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that if multiple methods are registered under the same name,
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
		/// <param name="headerLength">
		/// The number of bytes <paramref name="method"/> uses to read the tag
		/// header.
		/// </param>
		/// <param name="method">The method to add.</param>
		private static void Register(string format, uint headerLength, MethodInfo method) {
			if (method.IsPrivate)
				throw new NotSupportedException("Metadata format header-parsing functions must not be private");
			if (method.IsAbstract)
				throw new NotSupportedException("Metadata format header-parsing functions must not be abstract");
			if (method.IsStatic == false)
				throw new NotSupportedException("Metadata format header-parsing functions must be static");

			var parameters = method.GetParameters();
			if ((parameters.Length == 0)
				|| (typeof(IEnumerable<byte>).IsAssignableFrom(parameters[0].ParameterType) == false)
				|| ((parameters.Length > 1) && (parameters[1].IsOptional == false)))
				throw new NotSupportedException("Metadata format header-parsing functions must be able to take only an IEnumerable<byte>");

			if (typeof(MetadataTag).IsAssignableFrom(method.ReturnType) == false)
				throw new NotSupportedException("Metadata format header-parsing functions must return an empty instance of TagFormat");

			try {
				tagFormats.GetOrCreate(format).validationFunctions.Add(new FormatData.HeaderValidation<MetadataTag>() {
					length = (int)headerLength,
					function = method.CreateDelegate(typeof(FormatData.HeaderValidation<MetadataTag>.Validator))
						as FormatData.HeaderValidation<MetadataTag>.Validator
				});
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
