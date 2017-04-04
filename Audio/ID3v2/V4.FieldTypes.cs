/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using AgEitilt.Common.Stream;
using static AgEitilt.CardCatalog.Audio.ID3v2.ID3v23Plus.FormatFieldBases;

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
		public abstract class V4Field : V3PlusField<V4Field.V4VersionInfo> {
			/// <summary>
			/// Behaviours required for field initialization, specific to a
			/// particular version of the ID3v2 standard.
			/// </summary>
			public class V4VersionInfo : VersionInfo {
				/// <summary>
				/// The unique identifier for this version.
				/// </summary>
				public override string FormatName => format;

				/// <summary>
				/// The number of data bits used in the header field size bytes.
				/// </summary>
				public override uint FieldSizeBits => 7;

				/// <summary>
				/// Version-specific code for parsing field headers.
				/// </summary>
				/// 
				/// <param name="fieldObj">
				/// The new, boxed instance of the field.
				/// </param>
				/// <param name="header">The header top parse.</param>
				public override void Initialize(object fieldObj, IEnumerable<byte> header) {
					var field = fieldObj as V4Field;
					if (field == null)
						return;

					// Store this now, but it needs to be parsed when we get the
					// rest of the data
					field.flags = new BitArray(header.Skip(8).ToArray());
					field.FlagUnknown = field.flags.And(new BitArray(new byte[2] { 0b10001111, 0b10110000 })).Cast<bool>().Any();
				}
			}

			/// <summary>
			/// The flags set on the field.
			/// </summary>
			BitArray flags;
			/// <summary>
			/// Indicates that this field should be removed if the tag is
			/// edited in any way, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public override bool DiscardUnknownOnTagEdit => flags[1];
			/// <summary>
			/// Indicates that this field should be removed if the file
			/// external to the tag is edited in any way EXCEPT if the audio
			/// is completely replaced, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public override bool DiscardUnknownOnFileEdit => flags[2];
			/// <summary>
			/// Indicates that this field should not be changed without direct
			/// knowledge of its contents and structure.
			/// </summary>
			public override bool IsReadOnlyIfUnknown => flags[3];

			/// <summary>
			/// Indicates that the data in the field is compressed using the
			/// zlib compression scheme.
			/// </summary>
			public override bool IsFieldCompressed => flags[12];

			/// <summary>
			/// Indicates that the data in the field is encrypted according to
			/// a specified method.
			/// </summary>
			public override bool IsFieldEncrypted => flags[13];

			/// <summary>
			/// The group identifier for associating multiple fields, or
			/// `null` if this field isn't part of any group.
			/// </summary>
			byte? group = null;
			/// <summary>
			/// Indicates what group, if any, of fields this one belongs to.
			/// </summary>
			public override byte? IsFieldGrouped => group;

			/// <summary>
			/// Whether the header includes a non-standard flag, which may
			/// result in unrecognizable data.
			/// </summary>
			/// 
			/// <remarks>
			/// TODO: Store data about the unknown flags rather than simply
			/// indicating their presence.
			/// </remarks>
			protected bool FlagUnknown { get; private set; }

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
				/* Need to check the flag directly, as the property looks at
				 * `group` instead.
				 */
				if (flags[9]) {
					int b = stream.ReadByte();
					--Length;

					if (b >= 0)
						group = (byte)b;
				}

				// Compression
				bool zlib = IsFieldCompressed;

				// Encryption
				//TODO: Parse according to the ENCR frame
				byte? encryption = null;
				if (IsFieldEncrypted) {
					int b = stream.ReadByte();
					--Length;

					if (b >= 0)
						encryption = (byte)b;
				}

				// Data length; this may come into play in decompression and
				// decryption, but we currently have no use for it
				if (flags[15]) {
					var bytes = new byte[4];
					if (stream.ReadAll(bytes, 0, 4) == 4)
						Length = (int)ParseUnsignedInteger(bytes);
				}

				ParseData(stream);
			}

			/// <summary>
			/// Perform field-specific parsing after the required common
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
			/// A wrapper around an arbitrary <see cref="FieldBase"/>
			/// implementation.
			/// </summary>
			/// 
			/// <remarks>
			/// This is necessary because many ID3v2 fields are best described
			/// with a Venn diagram: the logic in displaying the body is the
			/// same for both v2.3 and v2.4, while the logic for parsing the
			/// header is shared with other tags of the <em>same</em> version.
			/// </remarks>
			public class V4FieldWrapper : V4Field {
				/// <summary>
				/// The core behaviour for this field.
				/// </summary>
				protected FieldBase fieldBase;

				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="inner">
				/// The underlying implementation to redirect calls to.
				/// </param>
				public V4FieldWrapper(FieldBase inner) =>
					fieldBase = inner;

				/// <summary>
				/// The byte header used to internally identify the field.
				/// </summary>
				public override byte[] SystemName =>
					fieldBase.SystemName;

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name =>
					fieldBase.Name;

				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle =>
					fieldBase.Subtitle;

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values =>
					fieldBase.Values;

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				/// 
				/// <param name="stream">The data to read.</param>
				protected override void ParseData(Stream stream) =>
					fieldBase.Parse(stream);

				/// <summary>
				/// Convert a ID3v2 byte representation of an encoding into the
				/// proper <see cref="Encoding"/> object.
				/// </summary>
				/// 
				/// <param name="enc">The encoding-identification byte.</param>
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
			}

			/// <summary>
			/// An embedded image.
			/// </summary>
			[TagField("APIC")]
			public class PictureField : V4FieldWrapper {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public PictureField(byte[] name, int length)
					: base(new PictureFieldBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// An identifier unique to a particular database.
			/// </summary>
			[TagField("UFID")]
			public class UniqueFileId : V4FieldWrapper {
				/// <summary>
				/// The constructor required to properly initialize the inner
				/// implementation of the <see cref="V4FieldWrapper"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public UniqueFileId(byte[] name, int length)
					: base(new UniqueFileIdBase(name, length)) { }
			}

			/// <summary>
			/// Any of the many tags containing purely textual data.
			/// </summary>
			[TagField]
			[TagField("XSOA")]
			[TagField("XSOP")]
			[TagField("XSOT")]
			public class TextFrame : V4FieldWrapper {
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
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <remarks>
				/// In order to properly use reflection, cannot solely use
				/// the version with a default parameter, as that can only be
				/// found with <see cref="System.Reflection.BindingFlags"/>
				/// introduced in .NETStandard 1.5, which is higher than I
				/// want to target.
				/// </remarks>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public TextFrame(byte[] name, int length)
					: this(name, length, null) { }
				/// <summary>
				/// The constructor required to properly initialize the inner
				/// implementation of the <see cref="V4FieldWrapper"/>.
				/// <para/>
				/// Used when the inheriting field does not exist in ID3v2.3.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				/// <param name="defaultName">
				/// The name to use if no more specific one is found, or
				/// <c>null</c> to use the default name as specified in the
				/// resources.
				/// </param>
				public TextFrame(byte[] name, int length, string defaultName)
					: base(new TextFrameBase(name, length, TryGetEncoding, defaultName)) { }

				/// <summary>
				/// The constructor required to properly initialize the inner
				/// implementation of the <see cref="V4FieldWrapper"/>.
				/// <para/>
				/// Used when the content behaviour is shared with ID3v2.3.
				/// </summary>
				/// 
				/// <param name="inner">
				/// The underlying implementation to redirect calls to.
				/// </param>
				public TextFrame(TextFrameBase inner) : base(inner) { }

				/// <summary>
				/// All strings contained within this field, still unboxed.
				/// </summary>
				protected IEnumerable<string> StringValues =>
					(fieldBase as TextFrameBase)?.StringValues;
			}

			/// <summary>
			/// A frame containing a number that may optionally be followed by
			/// a total count (eg. "Track 5 of 13").
			/// </summary>
			[TagField("TPOS")]
			[TagField("TRCK")]
			public class OfNumberFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public OfNumberFrame(byte[] name, int length)
					: base(new OfNumberFrameBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// A frame containing the ISRC of the recording.
			/// </summary>
			[TagField("TSRC")]
			public class IsrcFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public IsrcFrame(byte[] name, int length)
					: base(new IsrcFrameBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// A frame containing a mapping of role to person.
			/// </summary>
			/// 
			/// <remarks>
			/// These fields are new in ID3v2.4.
			/// <para/>
			/// TODO: This is a good candidate for allowing multiple subtitles
			/// in some form.
			/// </remarks>
			[TagField("TIPL")]
			[TagField("TMCL")]
			public class ListMappingFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public ListMappingFrame(byte[] name, int length)
					: base(name, length, Strings.ID3v24.Field_DefaultName_Credits) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						// Need easy "random" access for loop
						var valArray = StringValues?.ToArray();

						if ((valArray?.Length ?? 0) == 0) {
							yield return null;
						} else {
							// Easiest way to iterate over pairs of successive values
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
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public MsFrame(byte[] name, int length)
					: base(new MsFrameBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// A frame containing the musical key.
			/// </summary>
			[TagField("TKEY")]
			public class KeyFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public KeyFrame(byte[] name, int length)
					: base(new KeyFrameBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// A frame containing the language(s) sung/spoken.
			/// </summary>
			[TagField("TLAN")]
			public class LanguageFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public LanguageFrame(byte[] name, int length)
					: base(new LanguageFrameBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// A frame containing the genre.
			/// </summary>
			/// 
			/// <remarks>
			/// While similar to the ID3v2.3 tag, the syntax is sufficiently
			/// different to make it not worth sharing code.
			/// </remarks>
			[TagField("TCON")]
			public class GenreFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public GenreFrame(byte[] name, int length)
					: base(name, length) { }

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
						foreach (var s in StringValues) {
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
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public ResourceFrame(byte[] name, int length)
					: base(new ResourceFrameBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// A frame containing codes to look up from the resource files.
			/// </summary>
			/// 
			/// <remarks>
			/// If no matching string is found, the code/string will be
			/// displayed according to the standard "Unrecognized" format.
			/// <para/>
			/// The resource key must fit the pattern
			/// <c>Field_HEADER_Value</c> where <c>HEADER</c> is the unique
			/// header of the field, with the characters <c>/</c> and <c>.</c>
			/// replaced by <c>_</c>
			/// </remarks>
			[TagField("TCOP")]
			[TagField("TPRO")]
			public class ResourceValueFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public ResourceValueFrame(byte[] name, int length)
					: base(new ResourceValueFrameBase(name, length, TryGetEncoding)) { }
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
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public TimeFrame(byte[] name, int length)
					: base(new TimeFrameBase(name, length, TryGetEncoding, Strings.ID3v24.Field_DefaultName_Time)) { }
			}

			/// <summary>
			/// A frame containing encoder-defined text.
			/// </summary>
			[TagField("TXXX")]
			public class UserTextFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public UserTextFrame(byte[] name, int length)
					: base(new UserTextFrameBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// Any frame containing a URL.
			/// </summary>
			[TagField]
			public class UrlFrame : V4FieldWrapper {
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
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public UrlFrame(byte[] name, int length)
					: base(new UrlFrameBase(name, length)) { }
			}
			/// <summary>
			/// A frame containing an encoder-defined URL.
			/// </summary>
			[TagField("WXXX")]
			public class UserUrlFrame : V4FieldWrapper {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public UserUrlFrame(byte[] name, int length)
					: base(new UserUrlFrameBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// A frame containing text with a language, a description, and
			/// non-null-separated text.
			/// </summary>
			[TagField("COMM")]
			[TagField("USLT")]
			public class LongTextFrame : V4FieldWrapper {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public LongTextFrame(byte[] name, int length)
					: base(new LongTextFrameBase(name, length, TryGetEncoding)) { }
			}

			/// <summary>
			/// A frame containing a single binary counter.
			/// </summary>
			[TagField("PCNT")]
			public class CountFrame : V4FieldWrapper {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="name">
				/// The value to save to <see cref="TagField.SystemName"/>.
				/// </param>
				/// <param name="length">
				/// The value to save to <see cref="TagField.Length"/>.
				/// </param>
				public CountFrame(byte[] name, int length)
					: base(new CountFrameBase(name, length)) { }
			}
		}
	}
}
