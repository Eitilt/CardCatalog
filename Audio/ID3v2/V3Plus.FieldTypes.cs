using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AgEitilt.CardCatalog.Audio.ID3v2 {
	public partial class ID3v23Plus {
		/// <summary>
		/// Behaviours required for field initialization, specific to a
		/// particular version of the ID3v2 standard.
		/// </summary>
		public abstract class VersionInfo {
			/// <summary>
			/// The unique identifier for this version.
			/// </summary>
			public abstract string FormatName { get; }

			/// <summary>
			/// The number of data bits used in the header field size bytes.
			/// </summary>
			public abstract uint FieldSizeBits { get; }

			/// <summary>
			/// Version-specific code for parsing field headers.
			/// </summary>
			/// 
			/// <param name="fieldObj">
			/// The new, boxed instance of the field.
			/// </param>
			/// <param name="header">The header top parse.</param>
			public abstract void Initialize(object fieldObj, IEnumerable<byte> header);
		}

		/// <summary>
		/// Provide a base for fields sharing a common body between ID3v2.3
		/// and v2.4, to avoid defining them twice.
		/// </summary>
		/// 
		/// <typeparam name="TVersion">
		/// The ID3v2 version-specific code.
		/// </typeparam>
		public abstract class V3PlusField<TVersion> : TagField where TVersion : VersionInfo, new() {
			static TVersion version = new TVersion();

			/// <summary>
			/// Reduce the lookups of field types by caching the return.
			/// </summary>
			static IReadOnlyDictionary<byte[], Type> fields = MetadataFormat.FieldTypes(version.FormatName);

			/// <summary>
			/// Valid ID3v2 field identification characters.
			/// </summary>
			protected static char[] HeaderChars => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

			/// <summary>
			/// A byte sequence indicating that the "header" read is actually
			/// padding rather than data.
			/// </summary>
			static byte[] padding = new byte[4] { 0x00, 0x00, 0x00, 0x00 };

			/// <summary>
			/// Check whether the stream begins with a valid field header.
			/// </summary>
			/// 
			/// <param name="header">The sequence of bytes to check.</param>
			/// 
			/// <returns>
			/// An empty <see cref="TagField"/> object if the header is in the
			/// proper format, `null` otherwise.
			/// </returns>
			[HeaderParser(10)]
			public static TagField Initialize(IEnumerable<byte> header) {
				var bytes = header.Take(4).ToArray();
				int length = (int)ParseUnsignedInteger(header.Skip(4).Take(4).ToArray(), version.FieldSizeBits);

				if (bytes.SequenceEqual(padding)) {
					return null;
				} else if (fields.ContainsKey(bytes) == false) {
					return new TagField.Empty(bytes, length);
				}

				var field = Activator.CreateInstance(fields[bytes], new object[2] { bytes, length });
				if (field == null)
					return null;

				version.Initialize(field, header);
				return field as TagField;
			}

			/// <summary>
			/// Indicates that this field should be removed if the tag is
			/// edited in any way, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public abstract bool DiscardUnknownOnTagEdit { get; }

			/// <summary>
			/// Indicates that this field should be removed if the file
			/// external to the tag is edited in any way EXCEPT if the audio
			/// is completely replaced, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public abstract bool DiscardUnknownOnFileEdit { get; }

			/// <summary>
			/// Indicates that this field should not be changed without direct
			/// knowledge of its contents and structure.
			/// </summary>
			public abstract bool IsReadOnlyIfUnknown { get; }

			/// <summary>
			/// Indicates that the data in the field is compressed using the
			/// zlib compression scheme.
			/// </summary>
			public abstract bool IsFieldCompressed { get; }

			/// <summary>
			/// Indicates that the data in the field is encrypted according to
			/// a specified method.
			/// </summary>
			public abstract bool IsFieldEncrypted { get; }

			/// <summary>
			/// Indicates what group, if any, of fields this one belongs to.
			/// </summary>
			/// 
			/// <value>
			/// The number of the group, or <c>null</c> if the field is
			/// ungrouped.
			/// </value>
			public abstract byte? IsFieldGrouped { get; }
		}
	}
}
