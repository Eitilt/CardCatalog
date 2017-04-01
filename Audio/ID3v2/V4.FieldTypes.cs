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
		/// <para/>
		/// TODO: Migrate the lookup-in-resx Name implementations to here.
		/// </remarks>
		public abstract class V4Field : TagField {
			/// <summary>
			/// Reduce the lookups of field types by caching the return.
			/// </summary>
			static IReadOnlyDictionary<byte[], Type> fields = MetadataFormat.FieldTypes(format);

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
			/// The flags set on the field.
			/// </summary>
			BitArray flags;
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
			byte? group = null;

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
			 * Still need to check:
			 * http://musoware.com/wiki/index.php?title=Attribute_Mapping
			 * http://musicbee.wikia.com/wiki/Tag?useskin=monobook
			 */

			/*TODO: MCDI, ETCO, MLLT, SYTC, SYLT, RVA2, EQU2, RVRB,
			 * GEOB, POPM, RBUF, AENC, LINK, POSS, USER, OWNE, COMR, ENCR,
			 * GRID, PRIV, SIGN, SEEK, ASPI
			 * 
			 * Unofficial (most may never have a need for inclusion):
			 * GRP1, MVNM, MVIN, PCST, TSIZ, MCDI, ITNU, XDOR, XOLY
			 */

			/// <summary>
			/// An embedded image.
			/// </summary>
			[TagField(header)]
			public class PictureField : V4Field {
				/// <summary>
				/// The different roles an image may play relative to the file.
				/// </summary>
				/// 
				/// <remarks>
				/// These are taken from ID3v2 image categories, and the order should
				/// reflect that.
				/// </remarks>
				public enum ImageCategory : byte {
					/// <summary>
					/// Catchall for otherwise-undefined image types.
					/// </summary>
					Other = 0x00,
					/// <summary>
					/// A 32-pixel square icon to represent the file.
					/// </summary>
					/// 
					/// <remarks>
					/// Canonically (for the ID3v2 specification), this must be a PNG.
					/// </remarks>
					FileIcon = 0x01,
					/// <summary>
					/// An icon without the restrictions of <see cref="OtherIcon"/>.
					/// </summary>
					OtherIcon = 0x02,
					/// <summary>
					/// Front cover of, for example, the including album.
					/// </summary>
					/// 
					/// <seealso cref="CoverBack"/>
					CoverFront = 0x03,
					/// <summary>
					/// Back cover of, for example, the including album.
					/// </summary>
					CoverBack = 0x04,
					/// <summary>
					/// A page from the booklet included with the file source.
					/// </summary>
					Booklet = 0x05,
					/// <summary>
					/// The physical medium of the file source.
					/// </summary>
					Medium = 0x06,
					/// <summary>
					/// The primary artist or performer, or a soloist.
					/// </summary>
					/// 
					/// <seealso cref="Artist"/>
					ArtistMain = 0x07,
					/// <summary>
					/// Any single artist or performer.
					/// </summary>
					/// 
					/// <seealso cref="ArtistMain"/>
					/// <seealso cref="Band"/>
					Artist = 0x08,
					/// <summary>
					/// The orchestra or choir conductor.
					/// </summary>
					Conductor = 0x09,
					/// <summary>
					/// An image of the band or orchestra as a whole, rather than an
					/// individual performer.
					/// </summary>
					/// 
					/// <seealso cref="Artist"/>
					Band = 0x0A,
					/// <summary>
					/// The composer of the music.
					/// </summary>
					Composer = 0x0B,
					/// <summary>
					/// The lyrics or prose writer.
					/// </summary>
					Writer = 0x0C,
					/// <summary>
					/// The location where the work was recorded or written.
					/// </summary>
					Location = 0x0D,
					/// <summary>
					/// An image taken during (and of) the creation of the work, such
					/// as a recording session.
					/// </summary>
					/// 
					/// <seealso cref="Performance"/>
					Session = 0x0E,
					/// <summary>
					/// An image taken during a live performance of the work, but not
					/// necessarily the one this file is a recording of.
					/// </summary>
					Performance = 0x0F,
					/// <summary>
					/// A screen capture from a video or computer related to the file.
					/// </summary>
					ScreenCapture = 0x10,
					/// <summary>
					/// A brightly-colored fish, or other fun easter egg.
					/// </summary>
					BrightFish = 0x11,
					/// <summary>
					/// An illustration related to the work.
					/// </summary>
					Illustration = 0x12,
					/// <summary>
					/// The logo of the artist or band.
					/// </summary>
					LogoArtist = 0x13,
					/// <summary>
					/// The logo of the publisher or studio.
					/// </summary>
					LogoPublisher = 0x14
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
				public PictureField(byte[] name, int length) => Length = length;

				/// <summary>
				/// The easy representation of the field header.
				/// </summary>
				const string header = "APIC";
				/// <summary>
				/// The byte header used to internally identify the field.
				/// </summary>
				public override byte[] SystemName => ISO88591.GetBytes(header);

				/// <summary>
				/// What is depicted by the image.
				/// </summary>
				ImageCategory category;
				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Name => category.PrintableName();

				/// <summary>
				/// The uniquely identifying description of the image.
				/// </summary>
				string description;
				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle => base.Subtitle;

				/// <summary>
				/// The raw image data.
				/// </summary>
				ImageData image;
				/// <summary>
				/// The MIME type of the image.
				/// </summary>
				string mime;

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values =>
					new object[2] { image, mime };

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				/// 
				/// <param name="stream">The data to read.</param>
				protected override void ParseData(Stream stream) {
					Encoding encoding;
					var enc = stream.ReadByte();
					if (enc < 0)
						encoding = null;
					else
						encoding = TextFrame.TryGetEncoding((byte)enc);

					var read = new List<byte>();
					for (int b = stream.ReadByte(); b > 0x00; b = stream.ReadByte())
						read.Add((byte)b);
					mime = ISO88591.GetString(read.ToArray());

					var next = stream.ReadByte();
					if (next < 0)
						category = ImageCategory.Other;
					else
						category = (ImageCategory)next;

					read.Clear();
					var prevZero = false;
					for (int b = stream.ReadByte(); b >= 0x00; b = stream.ReadByte()) {
						if (b == 0x00) {
							if (encoding == ISO88591)
								break;
							else
								prevZero = true;
						} else {
							if (prevZero) {
								read.Add(0x00);
								prevZero = false;
							}
							read.Add((byte)b);
						}
					}
					var descriptionArray = read.ToArray();
					if (encoding == null)
						description = TextFrame.ReadFromByteOrderMark(descriptionArray);
					else
						description = encoding.GetString(descriptionArray);

					var dataLength = Length - 3 - mime.Length - descriptionArray.Length - (encoding == ISO88591 ? 1 : 2);
					var data = new byte[Length];
					int readCount = stream.ReadAll(data, 0, dataLength);

					image = new ImageData(data.Take(readCount).ToArray());
				}
			}

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
				const string header = "UFID";
				/// <summary>
				/// The byte header used to internally identify the field.
				/// </summary>
				public override byte[] SystemName => ISO88591.GetBytes(header);

				/// <summary>
				/// The database with which this ID is associated.
				/// </summary>
				string owner = null;
				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle => owner;


				/// <summary>
				/// The raw identifier.
				/// </summary>
				byte[] id = null;
				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						if ((id == null) || (id.Length == 0))
							return null;
						else
							return new object[] { id };
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
						/* Recognized nonstandard fields:
						 * TCAT: Unofficial (podcasts)
						 * TDES: Unofficial (podcasts)
						 * TGID: Unofficial (podcasts): "Podcast ID"
						 * TIT1: Official title: "Grouping"
						 * TKWD: Unofficial (podcasts)
						 * TOAL: "Work title" (Picard)
						 * TPUB: "Record label" (Picard)
						 * TSO2: Unofficial
						 * TSOC: Unofficial
						 */
						return Strings.ID3v24.ResourceManager.GetString("Field_" + ISO88591.GetString(header))
							?? DefaultName;
					}
				}
				/// <summary>
				/// The name to use if the header was not matched.
				/// </summary>
				protected virtual string DefaultName { get; } = Strings.ID3v24.Field_DefaultName_Text;

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
				public override IEnumerable<object> Values => values;

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
				internal static string ReadFromByteOrderMark(byte[] data) {
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
				protected static IEnumerable<string> SplitStrings(byte[] data, Encoding encoding) {
					var raw = (encoding == null ? ReadFromByteOrderMark(data) : encoding.GetString(data));
					var split = raw.Split(new char[1] { '\0' }, StringSplitOptions.None);

					var last = split.Length - 1;
					// Empty array shouldn't happen, but handle it just in case
					if (last < 0)
						return new string[1] { raw };

					// If the last string in the list is empty or null, remove
					// it, as the actual last string is typically terminated
					if ((split[last]?.Length ?? 0) == 0)
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
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = Strings.ID3v24.Field_DefaultName_Number;

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values => values.Select<string, object>(s => {
					var split = s?.Split(new char[1] { '/' }) ?? Array.Empty<string>();
					if (split.Length == 0) {
						return null;
					} else if (split.Length == 1) {
						if (int.TryParse(split[0], out var parsed))
							return parsed;
						else
							return s;
					} else {
						if (int.TryParse(split[0], out var parsed0) && int.TryParse(split[1], out var parsed1))
							return String.Format(Strings.ID3v24.Field_ValueFormat_Number, parsed0, parsed1);
						else
							return s;
					}
				}).Where(s => s != null);
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
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values => from isrc in values
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
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = Strings.ID3v24.Field_DefaultName_Credits;

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						// Need easy "random" access for loop
						var valArray = values?.ToArray();

						if (valArray?.Length == 0) {
							yield return null;

						// Easiest way to iterate over pairs of successive values
						} else {
							for (int i = 0, j = 1; i < valArray.Length; i += 2, j += 2) {
								// Singleton element without corresponding title/value
								if (j == valArray.Length)
									yield return String.Format(CardCatalog.Strings.Base.Field_DefaultValue, valArray[i]);
								// Credit title is empty
								else if (valArray[i].Length == 0)
									yield return String.Format(Strings.ID3v24.Field_Value_Credits_EmptyRole, valArray[j]);
								// Proper credit title/value pair
								else
									yield return String.Format(Strings.ID3v24.Field_Value_Credits, valArray[i], valArray[j]);
							}
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
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = Strings.ID3v24.Field_DefaultName_Length;

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						foreach (var s in values) {
							if (DateTime.TryParse(s, out var time))
								yield return time;
							else
								yield return String.Format(CardCatalog.Strings.Base.Field_DefaultValue, s);
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
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						foreach (var s in values) {
							var cs = s.ToCharArray();
							// Validate that the value is a properly-formatted key
							if (((s.Length >= 1) && (s.Length <= 3))
									&& "ABCDEFG".Contains(cs[0])
									&& ((s.Length < 2) || "b#m".Contains(cs[1]))
									&& ((s.Length < 3) || ('m' == cs[2])))
								yield return s.Replace('b', '\u266D').Replace('#', '\u266F');
							else
								yield return String.Format(CardCatalog.Strings.Base.Field_DefaultValue, s);
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
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Needs better ISO 639-2 lookup ability: see solution
				/// at http://stackoverflow.com/questions/12485626/replacement-for-cultureinfo-getcultures-in-net-windows-store-apps
				/// Might also be nice to add e.g. ISO 639-3 support in the
				/// same package ("CultureExtensions").
				/// </remarks>
				public override IEnumerable<object> Values => base.Values;
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
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Split "Remix" and "Cover" into separately-displayed
				/// field; likely same fix as <see cref="ListMappingFrame"/>.
				/// </remarks>
				public override IEnumerable<object> Values {
					get {
						foreach (var s in values) {
							if (s.Equals("RX"))
								yield return Strings.ID3v24.Field_TCON_RX;
							else if (s.Equals("CR"))
								yield return Strings.ID3v24.Field_TCON_CR;
							else if (s.All(char.IsDigit) && (s.Length <= 3)) {
								/* Given that everything is a digit and the
								 * length is capped, Parse is guaranteed to
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
			/// A frame containing codes to look up from the resource files.
			/// </summary>
			/// 
			/// <remarks>
			/// If no matching string is found, the code/string will be
			/// displayed according to the standard "Unrecognized" format.
			/// <para/>
			/// The resource key must fit the pattern <c>Field_HEADER_CODE</c>
			/// where <c>HEADER</c> is the unique header of the field and
			/// <c>CODE</c> is the string value. In both, the characters
			/// <c>/</c> and <c>.</c> will be replaced with <c>_</c>
			/// </remarks>
			[TagField("TCMP")]
			[TagField("TFLT")]
			[TagField("TMED")]
			public class ResourceFrame : TextFrame {
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
				public ResourceFrame(byte[] name, int length) : base(name, length) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Split "Remix" and "Cover" into separately-displayed
				/// field; likely same fix as <see cref="ListMappingFrame"/>.
				/// </remarks>
				public override IEnumerable<object> Values =>
					from keyBase in values
					// Replace non-key-safe characters
					select (keyBase, keyBase.Replace('/', '_').Replace('.', '_')) into keyValues
					// Compose the value into the proper format for lookup
					select Strings.ID3v24.ResourceManager.GetString("Field_" + ISO88591.GetString(header) + "_" + keyValues.Item2)
					// Fallback if key not found
						?? String.Format(CardCatalog.Strings.Base.Field_DefaultValue, keyValues.Item1);
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
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values => values.Select(s => String.Format(Strings.ID3v24.Field_TCOP_Value, s));
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
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values => values.Select(s => String.Format(Strings.ID3v24.Field_TPRO_Value, s));
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
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = Strings.ID3v24.Field_DefaultName_Time;

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						foreach (var s in values) {
							var times = new DateTimeOffset[2];
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
								if (DateTimeOffset.TryParse(
										time,
										CultureInfo.InvariantCulture,
										DateTimeStyles.AdjustToUniversal,
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

							if ((times[0] == null) || (times[0] == DateTimeOffset.MinValue))
								yield return Strings.ID3v24.Field_Time_Unknown;
							else if ((times[1] != null) && (times[1] != DateTimeOffset.MinValue))
								yield return String.Format(Strings.ID3v24.Field_Time_Range, times[0], times[1]);
							else
								//TODO: Only return the given values
								yield return String.Format(Strings.ID3v24.Field_Time_Single, times[0]);
						}
					}
				}
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
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle => values.FirstOrDefault();

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values => values.Skip(1);
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
						// WFED: Unofficial (podcast)
						return Strings.ID3v24.ResourceManager.GetString("Field_" + ISO88591.GetString(header))
							?? DefaultName;
					}
				}
				/// <summary>
				/// The name to use if the header was not matched.
				/// </summary>
				protected override string DefaultName { get; } = Strings.ID3v24.Field_DefaultName_Url;

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
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle => values.FirstOrDefault();

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values => values.Skip(1);
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
				string description = null;
				/// <summary>
				/// The language in which the lyrics are
				/// transcribed/translated.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Replace with <see cref="CultureInfo"/> object.
				/// </remarks>
				string language = null;

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name {
					get {
						return Strings.ID3v24.ResourceManager.GetString("Field_" + ISO88591.GetString(header))
							?? DefaultName;
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
							return String.Format(Strings.ID3v24.Field_Subtitle_Language, description, language);
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
				public override IEnumerable<object> Values => base.Values;

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
					// Unlike the standard text tags, this says nothing about
					// nulls, and so they should be restored
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
				const string header = "PCNT";
				/// <summary>
				/// The byte header used to internally identify the field.
				/// </summary>
				public override byte[] SystemName => ISO88591.GetBytes(header);

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name => Strings.ID3v24.Field_PCNT;

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
				ulong count = 0;
				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values => new object[1] { count };

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

	/// <summary>
	/// Extension methods for the <see cref="ImageData"/> class.
	/// </summary>
	public static class ImageCategoryExtension {
		/// <summary>
		/// Convert a <see cref="V4.FormatFields.PictureField.ImageCategory"/>
		/// value to a human-readable string for the current locale.
		/// </summary>
		/// 
		/// <param name="value">
		/// The <see cref="V4.FormatFields.PictureField.ImageCategory"/> to
		/// format.
		/// </param>
		/// 
		/// <returns>The formatted name.</returns>
		public static string PrintableName(this V4.FormatFields.PictureField.ImageCategory value) {
			var str = value.ToString();

			// If `str` is purely digits, the lookup has failed
			if (str.All(char.IsDigit) == false) {
				var genre = Strings.ID3v24.ResourceManager.GetString("Image_" + str);
				if (genre != null)
					return genre;
			}
			return String.Format(Strings.ID3v24.Image_Unknown, str);
		}
	}
}
