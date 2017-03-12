﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Metadata.Audio.ID3v2 {
	partial class V4 {
		/// <summary>
		/// Encapsulate parsing ID3v2.4 headers to reduce the functions each
		/// field is required to implement.
		/// </summary>
		/// 
		/// <remarks>
		/// This needs to be located between <see cref="TagField"/> and the
		/// field classes in the inheritance hierarchy in order for the type
		/// registration to recognize the <see cref="HeaderParserAttribute"/>.
		/// </remarks>
		public abstract class V4Field : TagField {
			/// <summary>
			/// Reduce the lookups of field types by caching the return.
			/// </summary>
			private static IReadOnlyDictionary<byte[], Type> fields = MetadataFormat.FieldTypes(format);

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
				int length = (int)ParseUnsignedInteger(header.Skip(4).Take(4).ToArray(), 7);

				TagField field;
				if (fields.ContainsKey(bytes)) {
					Type fieldType = fields[bytes];

					field = Activator.CreateInstance(fieldType, new object[2] { bytes, length }) as TagField;
				} else {
					field = new TagField.Empty(bytes, length);
				}

				//TODO: Handle flag bytes
				return field;
			}
		}

		/// <summary>
		/// Fields specific to the ID3v2.4 standard.
		/// </summary>
		public class FormatFields {
			/// <summary>
			/// An identifier unique to a particular database.
			/// </summary>
			[TagField(header)]
			public class UniqueFileId : V4Field {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public UniqueFileId(byte[] name, int length) => Length = length;

				/// <summary>
				/// The easy representation of the field header.
				/// </summary>
				private const string header = "UFID";
				/// <summary>
				/// The byte header used to internally identify the field.
				/// </summary>
				public override byte[] SystemName => ISO88591.GetBytes(header);

				/// <summary>
				/// The database with which this ID is associated.
				/// </summary>
				string owner = null;
				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Unique ID" + (owner == null ? " (" + owner + ")" : "");

				/// <summary>
				/// The raw identifier.
				/// </summary>
				byte[] id = null;
				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values {
					get {
						if ((id == null) || (id.Length == 0))
							return Array.Empty<string>();
						else
							return new[] { BitConverter.ToString(id).Replace('-', ' ') };
					}
				}
					
				/// <summary>
				/// Read a sequence of bytes in the manner appropriate to the specific
				/// type of field.
				/// </summary>
				/// 
				/// <param name="stream">The data to read.</param>
				public override void Parse(Stream stream) {
					var data = new byte[Length];
					int read = stream.ReadAll(data, 0, Length);

					owner = ISO88591.GetString(data.Take(read).TakeWhile(b => b != 0x00).ToArray());
					id = data.Take(read).Skip(owner.Length + 1).ToArray();
				}
			}
			
			/// <summary>
			/// Any of the many tags containing purely textual data.
			/// </summary>
			[TagField("TIT2")]
			public class TextFrame : V4Field {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public TextFrame(byte[] name, int length) {
					header = name;
					Length = length;
				}

				/// <summary>
				/// The raw field header.
				/// </summary>
				private byte[] header = null;
				/// <summary>
				/// The byte header used to internally identify the field.
				/// </summary>
				public override byte[] SystemName => header;

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name {
					get {
						switch (ISO88591.GetString(header)) {
							case "TIT2": return "Title";
							default:     return base.Name;
						}
					}
				}
					
				/// <summary>
				/// All strings contained within this field.
				/// </summary>
				private List<string> values = null;
				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values => values;

				/// <summary>
				/// Read a sequence of bytes according to the encoding implied
				/// by a byte-order-mark at the head.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Split this into its own class, as it includes
				/// support for more encodings than ID3v2 does.
				/// </remarks>
				///
				/// <param name="data">The bytes to decode.</param>
				/// 
				/// <returns>
				/// The decoded string, or `null` if <paramref name="data"/>
				/// isn't headed by a recognized byte-order-mark.
				/// </returns>
				private string ReadFromByteOrderMark(byte[] data) {
					if ((data?.Length ?? 0) == 0)
						return null;
					switch (data[0]) {
						case 0x00:
							if ((data.Length >= 4) && (data[1] == 0x00) && (data[2] == 0xFE) && (data[3] == 0xFF))
								return Encoding.GetEncoding("utf-32BE").GetString(data);
							goto default;
						case 0x2B:
							if ((data.Length >= 4) && (data[1] == 0x2F) && (data[2] == 0x76)) {
								switch (data[3]) {
									case 0x38:
									case 0x39:
									case 0x2B:
									case 0x2F:
										return Encoding.UTF7.GetString(data);
								}
							}
							goto default;
						case 0xEF:
							if ((data.Length >= 3) && (data[1] == 0xBB) && (data[2] == 0xBF))
								return Encoding.UTF8.GetString(data);
							goto default;
						case 0xFE:
							if ((data.Length >= 2) && (data[1] == 0xFF))
								return Encoding.BigEndianUnicode.GetString(data);
							goto default;
						case 0xFF:
							if ((data.Length >= 2) && (data[1] == 0xFE)) {
								if ((data.Length >= 4) && (data[2] == 0x00) && (data[3] == 0x00))
									return Encoding.UTF32.GetString(data);
								else
									return Encoding.Unicode.GetString(data);
							}
							goto default;
						default:
							return null;
					}
				}
					
				/// <summary>
				/// Parse a sequence of bytes as a list of null-separated
				/// strings.
				/// </summary>
				/// 
				/// <param name="data">The raw bytestream.</param>
				/// <param name="encoding">
				/// The text encoding to use in decoding <paramref name="data"/>,
				/// or `null` if each string begins with a byte order marker
				/// which may be used to detect the encoding dynamically.
				/// </param>
				/// <param name="termLength">
				/// The number of null bytes in the separator.
				/// </param>
				/// 
				/// <returns>The separated and parsed strings.</returns>
				protected List<string> SplitStrings(IEnumerable<byte> data, Encoding encoding, uint termLength) {
					var active = new List<byte>();
					var strings = new List<string>();
					byte zeros = 0;

					foreach (var b in data) {
						if (b == 0x00) {
							++zeros;
							if (zeros == termLength) {
								strings.Add(encoding?.GetString(active.ToArray()) ?? ReadFromByteOrderMark(active.ToArray()));
								active.Clear();
								zeros = 0;
							}
						} else {
							for (; zeros > 0; --zeros)
								active.Add(0x00);
							active.Add(b);
						}
					}

					for (; zeros > 0; --zeros)
						active.Add(0x00);
					if (active.Count > 0)
						strings.Add(encoding?.GetString(active.ToArray()) ?? ReadFromByteOrderMark(active.ToArray()));

					return strings;
				}
					
				/// <summary>
				/// Read a sequence of bytes in the manner appropriate to the
				/// specific type of field.
				/// </summary>
				/// 
				/// <param name="stream">The data to read.</param>
				public override void Parse(Stream stream) {
					var data = new byte[Length];
					// SplitStrings doesn't care about length, but shouldn't
					// be passed the unset tail
					int read = stream.ReadAll(data, 0, Length);
					if (read < Length)
						data = data.Take(read).ToArray();

					switch (data.First()) {
						case 0x00:
							values = SplitStrings(data.Skip(1), ISO88591, 1);
							break;
						case 0x01:
							values = SplitStrings(data.Skip(1), null, 2);
							break;
						case 0x02:
							values = SplitStrings(data.Skip(1), Encoding.BigEndianUnicode, 2);
							break;
						case 0x03:
							values = SplitStrings(data.Skip(1), Encoding.UTF8, 1);
							break;
						default:
							return;
					}
				}
			}
		}
	}
}
