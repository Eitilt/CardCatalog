/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using AgEitilt.Common.Stream;

namespace AgEitilt.CardCatalog.Audio.ID3v2 {
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
			/// Valid ID3v2 field identification characters.
			/// </summary>
			protected static char[] HeaderChars => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

			/// <summary>
			/// A byte sequence indicating that the "header" read is actually
			/// padding rather than data.
			/// </summary>
			private static byte[] padding = new byte[4] { 0x00, 0x00, 0x00, 0x00 };

			/// <summary>
			/// The flags set on the field.
			/// </summary>
			private BitArray flags;
			/// <summary>
			/// Indicates that this field should be removed if the tag is
			/// edited in any way, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public bool DiscardUnknownOnTagEdit => flags[1];
			/// <summary>
			/// Indicates that this field should be removed if the file
			/// external to the tag is edited in any way EXCEPT if the audio
			/// is completely replaced, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public bool DiscardUnknownOnFileEdit => flags[2];
			/// <summary>
			/// Indicates that this field should not be changed without direct
			/// knowledge of its contents and structure.
			/// </summary>
			public bool ReadOnlyIfUnknown => flags[3];

			/// <summary>
			/// Whether the header includes a non-standard tag, which may result
			/// in unrecognizable data.
			/// </summary>
			/// 
			/// <remarks>
			/// TODO: Store data about the unknown flags rather than simply
			/// indicating their presence.
			/// </remarks>
			protected bool FlagUnknown { get; private set; }

			/// <summary>
			/// The group identifier for associating multiple fields, or
			/// `null` if this field isn't part of any group.
			/// </summary>
			private byte? group = null;

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

				V4Field field;
				if (bytes.SequenceEqual(padding)) {
					return null;
				} else if (fields.ContainsKey(bytes) == false) {
					return new TagField.Empty(bytes, length);
				}

				field = Activator.CreateInstance(fields[bytes], new object[2] { bytes, length }) as V4Field;

				// Store this now, but it needs to be parsed when we get the
				// rest of the data
				field.flags = new BitArray(header.Skip(8).ToArray());
				field.FlagUnknown = field.flags.And(new BitArray(new byte[2] { 0b10001111, 0b10110000 })).Cast<bool>().Any();

				return field;
			}

			/// <summary>
			/// Read a sequence of bytes in the manner appropriate to the
			/// specific type of field.
			/// </summary>
			/// 
			/// <param name="stream">The data to read.</param>
			public sealed override void Parse(Stream stream) {
				// Unsynchronization
				/* Implied to affect the flag data bytes, unlike the
				 * compression and encryption
				 */
				if (flags[14]) {
					var bytes = new byte[Length];
					int read = stream.ReadAll(bytes, 0, Length);

					if (read < Length)
						bytes = bytes.Take(read).ToArray();

					Length -= bytes.Length;

					stream = new MemoryStream(DeUnsynchronize(bytes));
				}

				// Grouping
				if (flags[9]) {
					int b = stream.ReadByte();
					--Length;

					if (b >= 0)
						group = (byte)b;
				}

				// Compression
				bool zlib = flags[12];

				// Encryption
				//TODO: Parse according to the ENCR frame
				byte? encryption = null;
				if (flags[13]) {
					int b = stream.ReadByte();
					--Length;

					if (b >= 0)
						encryption = (byte)b;
				}

				// Data length; this may come into play in decompression and
				// decryption, but we currently have no use for it
				if (flags[14]) {
					var bytes = new byte[4];
					if (stream.ReadAll(bytes, 0, 4) == 4)
						Length = (int)ParseUnsignedInteger(bytes);
				}

				ParseData(stream);
			}

			/// <summary>
			/// Preform field-specific parsing after the required common
			/// parsing has been handled.
			/// </summary>
			/// 
			/// <param name="stream">The data to read.</param>
			protected abstract void ParseData(Stream stream);
		}

		/// <summary>
		/// Fields specific to the ID3v2.4 standard.
		/// </summary>
		public class FormatFields {
			/* Tag usage found at several websites:
			 * https://picard.musicbrainz.org/docs/mappings/
			 * https://msdn.microsoft.com/en-us/library/windows/desktop/dd743220(v=vs.85).aspx
			 * http://joelverhagen.com/blog/2010/12/how-itunes-uses-id3-tags/
			 * http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/ID3.html
			 * http://wiki.hydrogenaud.io/index.php?title=Tag_Mapping
			 * http://wiki.hydrogenaud.io/index.php?title=Foobar2000:ID3_Tag_Mapping
			 */

			/*TODO: MCDI, ETCO, MLLT, SYTC, SYLT, RVA2, EQU2, RVRB, APIC,
			 * GEOB, POPM, RBUF, AENC, LINK, POSS, USER, OWNE, COMR, ENCR,
			 * GRID, PRIV, SIGN, SEEK, ASPI
			 * 
			 * Unofficial (most may never have a need for inclusion):
			 * GRP1, MVNM, MVIN, PCST, TSIZ, MCDI, ITNU, XDOR, XOLY
			 */

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
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				/// 
				/// <param name="stream">The data to read.</param>
				protected override void ParseData(Stream stream) {
					var data = new byte[Length];
					int read = stream.ReadAll(data, 0, Length);

					owner = ISO88591.GetString(data.Take(read).TakeWhile(b => b != 0x00).ToArray());
					id = data.Take(read).Skip(owner.Length + 1).ToArray();
				}
			}

			/// <summary>
			/// Any of the many tags containing purely textual data.
			/// </summary>
			[TagField]
			[TagField("XSOA")]
			[TagField("XSOP")]
			[TagField("XSOT")]
			public class TextFrame : V4Field {
				/// <summary>
				/// Generate all text field headers that aren't handled by
				/// other specialized classes.
				/// </summary>
				/// 
				/// <returns>
				/// The field headers using default text formatting.
				/// </returns>
				[FieldNames]
				public static IEnumerable<byte[]> NameGenerator() {
					foreach (char b in HeaderChars) {
						foreach (char c in HeaderChars) {
							foreach (char d in HeaderChars) {
								// Individually-handled text tags
								switch (new string(new char[3] { b, c, d })) {
									case "CMP":  // Unofficial: iTunes Compilation
									case "CON":  // Genre
									case "COP":  // Copyright
									case "DEN":  // Encoding date
									case "DLY":  // Playlist delay
									case "DOR":  // Original release date
									case "DRC":  // Recording date         (de-facto: "Release date")
									case "DRL":  // Release date
									case "DTG":  // Tagging date
									case "FLT":  // Audio encoding
									case "IPL":  // Production credits
									case "LAN":  // Language
									case "LEN":  // Length
									case "KEY":  // Key
									case "MCL":  // Performer credits
									case "MED":  // Original medium
									case "POS":  // Disk number
									case "PRO":  // Production copyright
									case "RCK":  // Track number
									case "SRC":  // Recording ISRC
									case "XXX":  // (User text field)
										continue;
									default:
										yield return new byte[4] { (byte)'T', (byte)b, (byte)c, (byte)d };
										break;
								}
							}
						}
					}
				}

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
					// Remapping of unofficial to official names
					switch (ISO88591.GetString(name)) {
						case "XSOA":
						case "XSOP":
						case "XSOT":
							header = new byte[4] { 0x54, name[1], name[2], name[3] };
							break;
						default:
							header = name;
							break;
					}

					Length = length;
				}

				/// <summary>
				/// The raw field header.
				/// </summary>
				protected byte[] header = null;
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
							case "TALB": return "Album";
							case "TBPM": return "Beats per minute";
							case "TCAT": return "Category";                 // Unofficial (podcasts)
							case "TCOM": return "Composer";
							case "TDES": return "Description";              // Unofficial (podcasts)
							case "TENC": return "Encoder";
							case "TEXT": return "Author";
							case "TGID": return "Album ID";                 // Unofficial (podcasts): "Podcast ID"
							case "TIT1": return "Work";                     // Official title: "Grouping"
							case "TIT2": return "Title";
							case "TIT3": return "Subtitle";
							case "TKWD": return "Keywords";                 // Unofficial (podcasts)
							case "TMOO": return "Mood";
							case "TOAL": return "Original album";           // Picard: "Work title"
							case "TOFN": return "Original filename";
							case "TOLY": return "Original author";
							case "TOPE": return "Original artist";
							case "TOWN": return "Owner";
							case "TPE1": return "Artist";
							case "TPE2": return "Album artist";
							case "TPE3": return "Conductor";
							case "TPE4": return "Remixer";
							case "TPUB": return "Publisher";                // Picard: "Record label"
							case "TRSN": return "Station name";
							case "TRSO": return "Station owner";
							case "TSO2": return "Album artist sort order";  // Unofficial
							case "TSOA": return "Album sort order";
							case "TSOC": return "Composer sort order";      // Unofficial
							case "TSOP": return "Artist sort order";
							case "TSOT": return "Title sort order";
							case "TSSE": return "Encoding settings";
							case "TSST": return "Disk title";
							default: return DefaultName;
						}
					}
				}
				/// <summary>
				/// The name to use if the header was not matched.
				/// </summary>
				protected virtual string DefaultName { get; } = "Unknown text";

				/// <summary>
				/// Extra human-readable information describing the field, such as the
				/// "category" of a header with multiple realizations.
				/// </summary>
				public override string Subtitle {
					get {
						if (Name.Equals(DefaultName))
							return BaseName;
						else
							return base.Subtitle;
					}
				}

				/// <summary>
				/// Circumvent the title parsing for subclasses that know the
				/// field doesn't contain basic text data.
				/// </summary>
				protected string BaseName => base.Name;

				/// <summary>
				/// All strings contained within this field.
				/// </summary>
				protected IEnumerable<string> values = null;
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
				/// Convert a ID3v2 byte representation of an encoding into the
				/// proper <see cref="Encoding"/> object.
				/// </summary>
				/// 
				/// <param name="enc"></param>
				/// 
				/// <returns>
				/// The proper <see cref="Encoding"/>, or `null` if the encoding
				/// is either unrecognized or "Detect Unicode endianness from byte
				/// order marker."
				/// </returns>
				public static Encoding TryGetEncoding(byte enc) {
					switch (enc) {
						case 0x00:
							return ISO88591;
						case 0x02:
							return Encoding.BigEndianUnicode;
						case 0x03:
							return Encoding.UTF8;
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
				/// 
				/// <returns>The separated and parsed strings.</returns>
				protected IEnumerable<string> SplitStrings(byte[] data, Encoding encoding) {
					var strings = (encoding == null ? ReadFromByteOrderMark(data) : encoding.GetString(data));
					var split = strings.Split(new char[1] { '\0' }, StringSplitOptions.None);

					var last = split.Length - 1;
					// Empty array shouldn't happen, but handle it just in case
					if (last < 0)
						return split;

					if ((split[last] == null) || (split[last].Length == 0))
						return split.Take(last);
					else
						return split;
				}

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				/// 
				/// <param name="stream">The data to read.</param>
				protected override void ParseData(Stream stream) {
					var data = new byte[Length];
					// SplitStrings doesn't care about length, but shouldn't
					// be passed the unset tail if the stream ended early
					int read = stream.ReadAll(data, 0, Length);
					if (read < Length)
						data = data.Take(read).ToArray();

					values = SplitStrings(data.Skip(1).ToArray(), TryGetEncoding(data.First()));
				}
			}
			/// <summary>
			/// A frame containing the track number.
			/// </summary>
			[TagField("TPOS")]
			[TagField("TRCK")]
			public class OfNumberFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public OfNumberFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				/// 
				/// <remarks>
				/// The default case should never occur, but is provided for
				/// future-proofing purposes.
				/// </remarks>
				public override string Name {
					get {
						switch (ISO88591.GetString(header)) {
							case "TPOS": return "Disk number";
							case "TRCK": return "Track number";
							default: return DefaultName;
						}
					}
				}
				/// <summary>
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = "Unknown number";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values => values.Select(s => s.Replace("/", " of "));
			}
			/// <summary>
			/// A frame containing the track number.
			/// </summary>
			[TagField("TSRC")]
			public class IsrcFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public IsrcFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Recording ISRC";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values => from isrc in values
															  where isrc.Contains('-') == false
															  where isrc.Length == 12
															  select isrc.Insert(2, "-")
																		 .Insert(6, "-")
																		 .Insert(9, "-");
			}
			/// <summary>
			/// A frame containing a mapping of role to person.
			/// </summary>
			/// 
			/// <remarks>
			/// TODO: This is a good candidate for allowing multiple subtitles
			/// in some form.
			/// </remarks>
			[TagField("TIPL")]
			[TagField("TMCL")]
			public class ListMappingFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public ListMappingFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				/// 
				/// <remarks>
				/// The default case should never occur, but is provided for
				/// future-proofing purposes.
				/// </remarks>
				public override string Name {
					get {
						switch (ISO88591.GetString(header)) {
							case "TIPL": return "Production credits";
							case "TMCL": return "Performer credits";
							default: return DefaultName;
						}
					}
				}
				/// <summary>
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = "Other credits";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values {
					get {
						var valArray = values.ToArray();
						for (int i = 0, j = 1; i < valArray.Length; i += 2, j += 2) {
							if (j == valArray.Length)
								yield return String.Format("{{ {0} }}", valArray[i]);
							else
								yield return (valArray[i] + (valArray[i].Length > 0 ? ": " : "") + valArray[j]);
						}
					}
				}
			}
			/// <summary>
			/// A frame containing a length of time, in milliseconds.
			/// </summary>
			[TagField("TDLY")]
			[TagField("TLEN")]
			public class MsFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public MsFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				/// 
				/// <remarks>
				/// The default case should never occur, but is provided for
				/// future-proofing purposes.
				/// </remarks>
				public override string Name {
					get {
						switch (ISO88591.GetString(header)) {
							case "TDLY": return "Playlist delay";
							case "TLEN": return "Length";
							default: return DefaultName;
						}
					}
				}
				/// <summary>
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = "Unknown length";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values {
					get {
						foreach (var s in values) {
							if (int.TryParse(s, out int ms)) {
								yield return TimeSpan.FromMilliseconds(ms).ToString();
							} else {
								yield return String.Format("{{ {0} }}", s);
							}
						}
					}
				}
			}
			/// <summary>
			/// A frame containing the musical key.
			/// </summary>
			[TagField("TKEY")]
			public class KeyFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public KeyFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Key";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values {
					get {
						foreach (var s in values) {
							var cs = s.ToCharArray();
							if (((s.Length >= 1) && (s.Length <= 3))
									&& "ABCDEFG".Contains(cs[0])
									&& ((s.Length < 2) || "b#m".Contains(cs[1]))
									&& ((s.Length < 3) || ('m' == cs[2])))
								yield return s.Replace('b', '\u266D').Replace('#', '\u266F');
							else
								yield return String.Format("{{ {0} }}", s);
						}
					}
				}
			}
			/// <summary>
			/// A frame containing the language sung/spoken.
			/// </summary>
			[TagField("TLAN")]
			public class LanguageFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public LanguageFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Language";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Needs better ISO 639-2 lookup ability: see solution
				/// at http://stackoverflow.com/questions/12485626/replacement-for-cultureinfo-getcultures-in-net-windows-store-apps
				/// Might also be nice to add e.g. ISO 639-3 support in the
				/// same package ("CultureExtensions").
				/// </remarks>
				public override IEnumerable<string> Values => base.Values;
			}
			/// <summary>
			/// A frame containing the genre.
			/// </summary>
			[TagField("TCON")]
			public class GenreFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public GenreFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Genre";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Split "Remix" and "Cover" into separately-displayed
				/// field; likely same fix as <see cref="ListMappingFrame"/>.
				/// </remarks>
				public override IEnumerable<string> Values {
					get {
						foreach (var s in values) {
							if (s.Equals("RX"))
								yield return "Remix";
							else if (s.Equals("CR"))
								yield return "Cover";
							else if (s.All(char.IsDigit) && (s.Length <= 3)) {
								/* Between everything being a digit and the
								 * length being capped, Parse is guaranteed to
								 * not throw an exception with the larger
								 * datatype.
								 */
								var num = uint.Parse(s);
								if (num > byte.MaxValue)
									yield return s;
								else
									yield return ((ID3v1.Genre)num).PrintableName();
							} else
								yield return s;
						}
					}
				}
			}
			/// <summary>
			/// A frame containing the genre.
			/// </summary>
			[TagField("TFLT")]
			public class FiletypeFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public FiletypeFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Audio encoding";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Split "Remix" and "Cover" into separately-displayed
				/// field; likely same fix as <see cref="ListMappingFrame"/>.
				/// </remarks>
				public override IEnumerable<string> Values {
					get {
						foreach (var s in values) {
							switch (s) {
								case "MIME":
									yield return "MIME type as follows";
									break;
								case "MPG":
									yield return "MPEG audio";
									break;
								case "MPG/1":
									yield return "MPEG 1/2 layer I";
									break;
								case "MPG/2":
									yield return "MPEG 1/2 layer II";
									break;
								case "MPG/2.5":
									yield return "MPEG 2.5";
									break;
								case "MPG/3":
									yield return "MPEG 1/2 layer III";
									break;
								case "MPG/AAC":
									yield return "Advanced audio compression (MPEG)";
									break;
								case "VQF":
									yield return "Transform-domain weighted interleave vector quantisation";
									break;
								case "PCM":
									yield return "Pulse code modulated audio";
									break;
								default:
									yield return s;
									break;
							}
						}
					}
				}
			}
			/// <summary>
			/// A frame containing the genre.
			/// </summary>
			[TagField("TMED")]
			public class MediumFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public MediumFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Original medium";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Split "Remix" and "Cover" into separately-displayed
				/// field; likely same fix as <see cref="ListMappingFrame"/>.
				/// </remarks>
				public override IEnumerable<string> Values {
					get {
						foreach (var s in values) {
							switch (s) {
								case "DIG":
									yield return "Unknown digital source";
									break;
								case "DIG/A":
									yield return "Analog transfer from digital";
									break;
								case "ANA":
									yield return "Unknown analog source";
									break;
								case "ANA/WAC":
									yield return "Wax cylinder";
									break;
								case "ANA/8CA":
									yield return "8-track tape cassette";
									break;
								case "CD":
									yield return "Compact disk";
									break;
								case "CD/A":
									yield return "Analog transfer from CD";
									break;
								case "CD/DDD":
									yield return "Compact disk (SPARS DDD)";
									break;
								case "CD/ADD":
									yield return "Compact disk (SPARS ADD)";
									break;
								case "CD/AAD":
									yield return "Compact disk (SPARS AAD)";
									break;
								case "LD":
									yield return "Laserdisk";
									break;
								case "TT":
									yield return "Turntable record";
									break;
								case "TT/33":
									yield return "33.33 rpm record";
									break;
								case "TT/45":
									yield return "45 rpm record";
									break;
								case "TT/71":
									yield return "71.29 rpm record";
									break;
								case "TT/76":
									yield return "76.59 rpm record";
									break;
								case "TT/78":
									yield return "78.26 rpm record";
									break;
								case "TT/80":
									yield return "80 rpm record";
									break;
								case "MD":
									yield return "MiniDisc";
									break;
								case "MD/A":
									yield return "Analog transfer from MiniDisc";
									break;
								case "DAT":
									yield return "DAT cassette";
									break;
								case "DAT/A":
									yield return "Analog transfer from DAT cassette";
									break;
								case "DAT/1":
									yield return "DAT standard: 48 kHz/16 bits, linear";
									break;
								case "DAT/2":
									yield return "DAT mode 2: 32 kHz/16 bits, linear";
									break;
								case "DAT/3":
									yield return "DAT mode 3: 32 kHz/12 bits, non-linear, low speed";
									break;
								case "DAT/4":
									yield return "DAT mode 4: 32 kHz/12 bits, 4 channels";
									break;
								case "DAT/5":
									yield return "DAT mode 5: 44.1 kHz/16 bits, linear";
									break;
								case "DAT/6":
									yield return "DAT mode 6: 44.1 kHz/16 bits, 'wide track' play";
									break;
								case "DCC":
									yield return "DCC cassette";
									break;
								case "DCC/A":
									yield return "Analog transfer from DCC cassette";
									break;
								case "DVD":
									yield return "DVD";
									break;
								case "DVD/A":
									yield return "Analog transfer from DVD";
									break;
								case "TV":
									yield return "Television";
									break;
								case "TV/PAL":
									yield return "Television (PAL)";
									break;
								case "TV/NTSC":
									yield return "Television (NTSC)";
									break;
								case "TV/SECAM":
									yield return "Television (SECAM)";
									break;
								case "VID":
									yield return "Video";
									break;
								case "VID/PAL":
									yield return "Video (PAL)";
									break;
								case "VID/NTSC":
									yield return "Video (NTSC)";
									break;
								case "VID/SECAM":
									yield return "Video (SECAM)";
									break;
								case "VID/VHS":
									yield return "VHS tape";
									break;
								case "VID/SVHS":
									yield return "S-VHS tape";
									break;
								case "VID/BETA":
									yield return "BETAMAX tape";
									break;
								case "RAD":
									yield return "Radio";
									break;
								case "RAD/FM":
									yield return "FM radio";
									break;
								case "RAD/AM":
									yield return "AM radio";
									break;
								case "RAD/LW":
									yield return "Longwave radio";
									break;
								case "RAD/MW":
									yield return "Medium wave radio";
									break;
								case "TEL":
									yield return "Telephone";
									break;
								case "TEL/I":
									yield return "ISDN telephone";
									break;
								case "MC":
									yield return "Tape cassette";
									break;
								case "MC/4":
									yield return "4.75 cm/s cassette";
									break;
								case "MC/9":
									yield return "9.5 cm/s cassette";
									break;
								case "MC/I":
									yield return "Type I (ferric) cassette";
									break;
								case "MC/II":
									yield return "Type II (chrome) cassette";
									break;
								case "MC/III":
									yield return "Type III (ferric chrome) cassette";
									break;
								case "MC/IV":
									yield return "Type IV (metal) cassette";
									break;
								case "REE":
									yield return "Tape cassette";
									break;
								case "REE/9":
									yield return "9.5 cm/s cassette";
									break;
								case "REE/19":
									yield return "19 cm/s cassette";
									break;
								case "REE/38":
									yield return "38 cm/s cassette";
									break;
								case "REE/76":
									yield return "76 cm/s cassette";
									break;
								case "REE/I":
									yield return "Type I (ferric) cassette";
									break;
								case "REE/II":
									yield return "Type II (chrome) cassette";
									break;
								case "REE/III":
									yield return "Type III (ferric chrome) cassette";
									break;
								case "REE/IV":
									yield return "Type IV (metal) cassette";
									break;
								default:
									yield return s;
									break;
							}
						}
					}
				}
			}
			/// <summary>
			/// A frame containing copyright information.
			/// </summary>
			[TagField("TCOP")]
			public class CopyrightFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public CopyrightFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Copyright";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values => values.Select(s => "\u00A9 " + s);
			}
			/// <summary>
			/// A frame containing copyright information.
			/// </summary>
			[TagField("TPRO")]
			public class PCopyrightFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public PCopyrightFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Production copyright";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values => values.Select(s => "\u2117 " + s);
			}
			/// <summary>
			/// A frame containing a timestamp.
			/// </summary>
			[TagField("TDEN")]
			[TagField("TDOR")]
			[TagField("TDRC")]
			[TagField("TDRL")]
			[TagField("TDTG")]
			public class TimeFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public TimeFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				/// 
				/// <remarks>
				/// The default case should never occur, but is provided for
				/// future-proofing purposes.
				/// </remarks>
				public override string Name {
					get {
						switch (ISO88591.GetString(header)) {
							case "TDEN": return "Encoding date";
							case "TDOR": return "Original release date";
							case "TDRC": return "Recording date";
							case "TDRL": return "Release date";
							case "TDTG": return "Tagging date";
							default: return DefaultName;
						}
					}
				}
				/// <summary>
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = "Unknown time";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values {
					get {
						foreach (var s in values) {
							var times = new DateTime[2];
							/*TODO: ID3v2 describes timestamps as a "subset"
							 * of ISO 8601, but may still want to support
							 * interval notation for additional robustness
							 */

							bool first = true;
							foreach (var time in s.Split(new string[2] { "/", "--" }, StringSplitOptions.RemoveEmptyEntries)) {
								/* This will lose data if more than two
								 * timestamps are included, but that violates
								 * ISO 8601 anyway
								 */
								if (DateTime.TryParse(
										time,
										CultureInfo.InvariantCulture,
										DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
										out times[first ? 0 : 1]
										) == false) {
									times = null;
									break;
								}
								first = false;
							}

							if (times == null) {
								yield return s;
								continue;
							}

							string ret;
							if ((times[0] == null) || (times[0] == DateTime.MinValue))
								ret = "Unknown";
							else
								//TODO: Only return the given values
								ret = times[0].ToString();

							if ((times[1] != null) && (times[1] != DateTime.MinValue)) {
								//TODO: Localize separator
								ret += "\u2013";

								//TODO: Only return the given values
								ret += times[1].ToString();
							}
							yield return ret;
						}
					}
				}
			}
			/// <summary>
			/// A frame containing the "Compilation" flag defined by iTunes.
			/// </summary>
			[TagField("TCMP")]
			public class ITunesCompilationFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public ITunesCompilationFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Compilation (iTunes)";

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values => from cmp in values
															  select (cmp.Equals("0")
																		 ? "Not part of a compilation"
																		 : (cmp.Equals("1")
																			   ? "From a compilation album"
																			   : String.Format("{{ {0} }}", cmp)));
			}
			/// <summary>
			/// A frame containing encoder-defined text.
			/// </summary>
			[TagField("TXXX")]
			public class UserTextFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public UserTextFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Other text";

				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle => values.FirstOrDefault();

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values => values.Skip(1);
			}

			/// <summary>
			/// Any frame containing a URL. By the specification, these differ
			/// from the base text by having a maximum of one occurrence and
			/// only allowing the ISO-8859-1 encoding.
			/// </summary>
			[TagField]
			public class UrlFrame : TextFrame {
				/// <summary>
				/// Generate all text field headers that aren't handled by
				/// other specialized classes.
				/// </summary>
				/// 
				/// <returns>
				/// The field headers using default text formatting.
				/// </returns>
				[FieldNames]
				public static new IEnumerable<byte[]> NameGenerator() {
					foreach (char b in HeaderChars) {
						foreach (char c in HeaderChars) {
							foreach (char d in HeaderChars) {
								// Individually-handled URL tag
								if ((b == 'X') && (c == 'X') && (d == 'X'))
									continue;
								else
									yield return new byte[4] { (byte)'W', (byte)b, (byte)c, (byte)d };
							}
						}
					}
				}

				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public UrlFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name {
					get {
						switch (ISO88591.GetString(header)) {
							case "WCOM": return "Purchasing information";
							case "WCOP": return "Copyright information";
							case "WFED": return "Feed";                    // Unofficial: MP3Tag/iTunes "Podcast URL"
							case "WOAF": return "Official website";
							case "WOAR": return "Artist homepage";
							case "WOAS": return "Source website";
							case "WORS": return "Radio station homepage";
							case "WPAY": return "Payment website";
							case "WPUB": return "Publisher homepage";
							default: return DefaultName;
						}
					}
				}
				/// <summary>
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = "Unknown URL";

				/// <summary>
				/// Read a sequence of bytes in the manner appropriate to the
				/// specific type of field.
				/// </summary>
				/// 
				/// <param name="stream">The data to read.</param>
				protected override void ParseData(Stream stream) {
					var data = new byte[Length];
					// SplitStrings doesn't care about length, but shouldn't
					// be passed the unset tail if the stream ended early
					int read = stream.ReadAll(data, 0, Length);
					if (read < Length)
						data = data.Take(read).ToArray();

					values = SplitStrings(data, ISO88591);
				}
			}
			/// <summary>
			/// A frame containing an encoder-defined URL.
			/// </summary>
			[TagField("WXXX")]
			public class UserUrlFrame : UrlFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public UserUrlFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Other URL";

				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle => values.FirstOrDefault();

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values => values.Skip(1);
			}

			/// <summary>
			/// A frame containing text with a language, a description, and
			/// non-null-separated text.
			/// </summary>
			[TagField("COMM")]
			[TagField("USLT")]
			public class LongTextFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public LongTextFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// The description of the lyrics instance.
				/// </summary>
				private string description = null;
				/// <summary>
				/// The language in which the lyrics are
				/// transcribed/translated.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Replace with <see cref="CultureInfo"/> object.
				/// </remarks>
				private string language = null;

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name {
					get {
						switch (ISO88591.GetString(SystemName)) {
							case "COMM": return "Comment";
							case "USLT": return "Lyrics";
							default: return DefaultName;
						}
					}
				}

				/// <summary>
				/// Extra human-readable information describing the field, such as the
				/// "category" of a header with multiple realizations.
				/// </summary>
				public override string Subtitle {
					get {
						if ((description == null) || (language == null))
							return null;
						else
							return String.Format("{0} ({1})", description, language);
					}
				}

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Needs better ISO 639-2 lookup ability: see solution
				/// at http://stackoverflow.com/questions/12485626/replacement-for-cultureinfo-getcultures-in-net-windows-store-apps
				/// Might also be nice to add e.g. ISO 639-3 support in the
				/// same package ("CultureExtensions").
				/// </remarks>
				public override IEnumerable<string> Values => base.Values;

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				/// 
				/// <param name="stream">The data to read.</param>
				protected override void ParseData(Stream stream) {
					var bytes = new byte[4];
					if (stream.ReadAll(bytes, 0, 4) < 4)
						return;

					language = ISO88591.GetString(bytes, 1, 3);

					var content = new byte[Length];
					int read = stream.ReadAll(content, 0, Length);
					if (read < Length)
						content = content.Take(read).ToArray();

					var split = SplitStrings(content, TryGetEncoding(bytes[0]));
					description = split.First();
					// Unlike the text tags, this says nothing about nulls
					values = new string[1] { String.Join("\0", split.Skip(1)) };
				}
			}

			/// <summary>
			/// A frame containing a single binary counter.
			/// </summary>
			[TagField(header)]
			public class CountFrame : V4Field {
				/// <summary>
				/// The constructor required by
				/// <see cref="V4Field.Initialize(IEnumerable{byte})"/>. This
				/// should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TextFrame.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public CountFrame(byte[] name, int length) => Length = length;

				/// <summary>
				/// The easy representation of the field header.
				/// </summary>
				private const string header = "PCNT";
				/// <summary>
				/// The byte header used to internally identify the field.
				/// </summary>
				public override byte[] SystemName => ISO88591.GetBytes(header);

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => "Play count";

				/// <summary>
				/// The value contained by this field.
				/// </summary>
				/// 
				/// <remarks>
				/// The specification implements a potentially-infinite
				/// integer, but a `ulong` should in theory never overflow
				/// given the effort required to play one file of a one song
				/// 18,446,744,073,709,551,615 times.
				/// 
				/// TODO: Probably should allow that
				/// 18,446,744,073,709,551,616th play anyway.
				/// </remarks>
				private ulong count = 0;
				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<string> Values => new string[1] { count.ToString() };

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				/// 
				/// <param name="stream">The data to read.</param>
				protected override void ParseData(Stream stream) {
					var bytes = new byte[Length];
					int read = stream.ReadAll(bytes, 0, Length);

					if (read < Length)
						bytes = bytes.Take(read).ToArray();

					count = ParseUnsignedInteger(bytes);
				}
			}
		}
	}
}
