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
		/// <summary>
		/// Validation functions for each registered metadata format.
		/// </summary>
		private static Dictionary<string, Tuple<int, Func<byte[], TagFormat>>> tagHeaderFunctions =
			new Dictionary<string, Tuple<int, Func<byte[], TagFormat>>>();

		/// <summary>
		/// The class encapsulating each registered metadata format.
		/// </summary>
		private static Dictionary<string, Type> tagFormats = new Dictionary<string, Type>();

		/// <summary>
		/// Maintain a single instance of the comparer rather than creating a new
		/// one for each dictionary.
		/// </summary>
		/// 
		/// <seealso cref="fieldTypes"/>
		private static FieldDictionary.SequenceEqualityComparer<byte> fieldKeyComparer = new FieldDictionary.SequenceEqualityComparer<byte>();
		/// <summary>
		/// Store lookup tables for the fields of each of the registered
		/// metadata formats.
		/// </summary>
		private static Dictionary<string, Dictionary<byte[], Type>> fieldTypes = new Dictionary<string, Dictionary<byte[], Type>>();

		/// <summary>
		/// A registry of previously-scanned assemblies in order to prevent
		/// unnecessary use of reflection methods.
		/// </summary>
		private static HashSet<string> assemblies = new HashSet<string>();

		/// <summary>
		/// Automatically add any 
		/// </summary>
		/// 
		/// <seealso cref="RefreshFormats"/>
		static MetadataFormat() {
			RefreshFormats<TagFormat>();
		}

		/// <summary>
		/// Scan the aseembly enclosing the specified for implementations of
		/// metadata formats.
		/// </summary>
		/// 
		/// <remarks>
		/// The explicit type parameter is preferred over scanning loaded assemblies
		/// as oftentimes this will be the first time the 
		/// </remarks>
		/// 
		/// <typeparam name="T">
		/// A type from the assembly to scan, extending <see cref="TagFormat"/>.
		/// </typeparam>
		/*TODO: Could be nice to scan all referenced assemblies (loaded or
		 * not) and load any with the attribute that aren't yet, to avoid
		 * needing to refresh manually. See .NET Core's AssemblyLoadContext.
		 */
		public static void RefreshFormats<T>() where T : TagFormat {
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
				if (tagAttr == null)
					continue;
				else
					Register(tagAttr.Name, t);

				var fieldAttr = t.GetTypeInfo().GetCustomAttribute<TagFieldAttribute>(false);
				if (fieldAttr == null)
					continue;
				else
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
		/// <param name="format">The type to add.</param>
		/// 
		/// <seealso cref="HeaderParserAttribute"/>
		public static void Register(Type format) {
			var attr = format.GetTypeInfo().GetCustomAttribute<MetadataFormatAttribute>(false);
			if (attr == null)
				throw new TypeLoadException("No explicit format name was passed, and the type has no attribute to infer it from");
			else
				Register(attr.Name, format);
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
		/// <param name="name">
		/// A short name for the format to be used as an access key for later
		/// lookups.
		/// <para/>
		/// It is recommended that this also be exposed as a constant.
		/// </param>
		/// <param name="format">The type to add.</param>
		public static void Register(string name, Type format) {
			if (typeof(TagFormat).IsAssignableFrom(format) == false)
				throw new NotSupportedException("Metadata format types must implement ITagFormat");

			tagFormats[name] = format;

			foreach (var method in format.GetRuntimeMethods()
					.Where((m) => m.IsDefined(typeof(HeaderParserAttribute))))
				Register(name, method.GetCustomAttribute<HeaderParserAttribute>().HeaderLength, method);
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

			if (format == null) {
				var enclosingType = fieldType.DeclaringType;
				if (enclosingType == null)
					throw new NotSupportedException("If a TagFieldAttribute does not declare a Format, it must be located within an enclosing type");

				var formatAttr = enclosingType.GetTypeInfo().GetCustomAttribute(typeof(MetadataFormatAttribute)) as MetadataFormatAttribute;
				if (formatAttr == null)
					throw new MissingFieldException("If a TagFieldAttribute does not declare a Format,"
						+ " the enclosing type must have an attached MetadataFormatAttribute");

				format = formatAttr.Name;
			}

			Dictionary<byte[], Type> dictionary;
			if (fieldTypes.ContainsKey(format)) {
				dictionary = fieldTypes[format];
			} else {
				dictionary = new Dictionary<byte[], Type>(fieldKeyComparer);
				fieldTypes[format] = dictionary;
			}

			dictionary[header] = fieldType;
		}
		
		/// <summary>
		/// Add the given method to the lookup tables under the specified
		/// custom descriptor.
		/// <para/>
		/// This should almost purely be called via
		/// <see cref="Register(string, Type)"/>, and has been separated
		/// primarily for code clarity.
		/// </summary>
		/// 
		/// <remarks>
		/// Note that if multiple methods are registered under the same name,
		/// any later registrations will override the previous.
		/// </remarks>
		/// 
		/// <param name="name">
		/// A short name for the format to be used as an access key for later
		/// lookups.
		/// </param>
		/// <param name="headerLength">
		/// The number of bytes <paramref name="method"/> uses to read the tag
		/// header.
		/// </param>
		/// <param name="method">The method to add.</param>
		private static void Register(string name, int headerLength, MethodInfo method) {
			if (method.IsPrivate)
				throw new NotSupportedException("Metadata format header-parsing functions must not be private");
			if (method.IsAbstract)
				throw new NotSupportedException("Metadata format header-parsing functions must not be abstract");
			if (method.IsStatic == false)
				throw new NotSupportedException("Metadata format header-parsing functions must be static");

			var parameters = method.GetParameters();
			if ((parameters.Length == 0)
				|| (typeof(byte[]).IsAssignableFrom(parameters[0].ParameterType) == false)
				|| ((parameters.Length > 1) && (parameters[1].IsOptional == false)))
				throw new NotSupportedException("Metadata format header-parsing functions must be able to take only a byte[]");

			if (typeof(TagFormat).IsAssignableFrom(method.ReturnType) == false)
				throw new NotSupportedException("Metadata format header-parsing functions must return an empty instance of TagFormat");

			tagHeaderFunctions[name] = Tuple.Create(headerLength, (Func<byte[], TagFormat>)method.CreateDelegate(typeof(Func<byte[], TagFormat>)));
		}

		/// <summary>
		/// Check the stream against all registered tag formats, and return
		/// those that match the header.
		/// </summary>
		/// 
		/// <remarks>
		/// While, in theory, only a single header should match, the class
		/// structure is such that this is not a restriction; supporting this
		/// feature allows for nonstandard usages without exclusive headers.
		/// 
		/// The callee is left to determine the best means of handling the
		/// case of Detect(...).Count > 1.
		/// </remarks>
		/// 
		/// <param name="stream">The bytestream to test.</param>
		/// 
		/// <returns>The keys of all matching formats.</returns>
		public async static Task<List<TagFormat>> Parse(Stream stream) {
			var tasks = new List<Task>();
			var ret = new List<TagFormat>();

			//TODO: It may be best to return an object explicitly combining
			// all recognized tags along with unknown data.

			bool foundTag = true;
			while (foundTag) {
				foundTag = false;

				// Keep track of all bytes read for headers, to avoid
				// unnecessarily repeating stream accesses
				var readBytes = new List<byte>();
				foreach (var header in tagHeaderFunctions) {
					// Make sure we have enough bytes to check the header
					int headerLength = header.Value.Item1;
					// Automatically leaves the stream untouched if `header`
					// is already longer than `headerLength`
					for (int i = readBytes.Count; i < headerLength; ++i) {
						int b = stream.ReadByte();
						if (b < 0)
							// If the stream has ended before the entire
							// header can fit, then the header's not present
							continue;
						else
							readBytes.Add((byte)b);
					}

					// Try to construct an empty tag of the current format
					var tag = header.Value.Item2(readBytes.Take(headerLength).ToArray());
					if (tag == null)
						// The header was invalid, and so this tag isn't the
						// correct type to parse next
						continue;

					ret.Add(tag);

					if (tag.Length == 0) {
						/* The header doesn't contain length data, so we need
                         * to read the stream directly until whatever end-of-
                         * tag the format uses is reached
                         */
						//BUG: This won't include any bytes already read for
						// the header
						//TODO: This should probably also be async
						tag.Parse(new BinaryReader(stream));
					} else {
						// Read the entire tag from the stream before parsing
						// the next tag along
						var bytes = new byte[tag.Length];
						// Restore any bytes previously read for the header
						readBytes.Skip(headerLength).ToArray().CopyTo(bytes, 0);
						int offset = (readBytes.Count - headerLength);

						/* In order to properly continue to the next tag in
                         * the file while the processing is performed as async
                         * we need to advance the stream beyond the current
                         * tag; given that Stream.Read*(...) isn't guaranteed
                         * to read the full count of bytes, we need to do this
                         * in a loop.
                         */
						while (offset < tag.Length) {
							var read = await stream.ReadAsync(bytes, offset, (int)(tag.Length - offset));

							if (read == 0)
								throw new EndOfStreamException("End of the stream was reached before the expected length of the final tag");
							else
								offset += read;
						}

						// Split off the processing to return to IO-bound code
						tasks.Add(tag.ParseAsync(bytes));
					}

					// All failure conditions were checked before this, so end
					// the "for the first matched `header`" loop
					foundTag = true;
					break;
				}
			}

			await Task.WhenAll(tasks);

			return ret;
		}
	}
}
