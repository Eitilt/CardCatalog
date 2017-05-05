/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using AgEitilt.Common.Stream.Extensions;
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
		/// </remarks>
		public abstract class V4Field : V3PlusField<V4Field.VersionInfo> {
			/// <summary>
			/// Behaviours required for field initialization, specific to a
			/// particular version of the ID3v2 standard.
			/// </summary>
			public class VersionInfo : ID3v23Plus.VersionInfo {
				/// <summary>
				/// The unique identifier for this version.
				/// </summary>
				public override string FormatName => format;

				/// <summary>
				/// The number of data bits used in the header field size
				/// bytes.
				/// </summary>
				public override uint FieldSizeBits => 7;

				/// <summary>
				/// Version-specific code for parsing field headers.
				/// </summary>
				/// 
				/// <param name="fieldObj">
				/// The new, boxed instance of the field.
				/// </param>
				/// <param name="header">The header to parse.</param>
				public override void Initialize(object fieldObj, IEnumerable<byte> header) {
					var field = fieldObj as V4Field;
					if (field == null)
						return;

					// Store this now, but it needs to be parsed when we get
					// the rest of the data
					field.Flags = new BitArray(header.Skip(8).ToArray());
					field.FlagUnknown = field.Flags.And(new BitArray(new byte[2] { 0b10001111, 0b10110000 })).Cast<bool>().Any();
				}
			}

			/// <summary>
			/// Indicates that this field should be removed if the tag is
			/// edited in any way, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public override bool DiscardUnknownOnTagEdit => Flags[1];
			/// <summary>
			/// Indicates that this field should be removed if the file
			/// external to the tag is edited in any way EXCEPT if the audio
			/// is completely replaced, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public override bool DiscardUnknownOnFileEdit => Flags[2];
			/// <summary>
			/// Indicates that this field should not be changed without direct
			/// knowledge of its contents and structure.
			/// </summary>
			public override bool IsReadOnlyIfUnknown => Flags[3];

			/// <summary>
			/// Indicates that the data in the field is compressed using the
			/// zlib compression scheme.
			/// </summary>
			public override bool IsFieldCompressed => Flags[12];

			/// <summary>
			/// Indicates that the data in the field is encrypted according to
			/// a specified method.
			/// </summary>
			public override bool IsFieldEncrypted => Flags[13];

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
			/// Read a sequence of bytes in the manner appropriate to the
			/// specific type of field.
			/// </summary>
			/// 
			/// <param name="stream">The data to read.</param>
			public sealed override void Parse(Stream stream) {
				base.Parse(stream);

				int extraHeaderBytes = 0;

				// Unsynchronization
				/* Implied to affect the flag data bytes, unlike the
				 * compression and encryption, so do it first
				 */
				if (Flags[14])
					Data = DeUnsynchronize(Data.Skip(extraHeaderBytes).ToArray());

				// Grouping
				/* Need to check the flag directly, as the property looks at
				 * `group` instead
				 */
				if (Flags[9]) {
					group = Data[extraHeaderBytes];
					Header = Header.Concat(new byte[1] { Data[extraHeaderBytes] }).ToArray();

					++extraHeaderBytes;
				}

				// Compression
				bool zlib = IsFieldCompressed;

				// Encryption
				//TODO: Parse according to the ENCR frame
				byte? encryption = null;
				if (IsFieldEncrypted) {
					encryption = Data[extraHeaderBytes];
					Header = Header.Concat(new byte[1] { Data[extraHeaderBytes] }).ToArray();

					++extraHeaderBytes;
				}

				// Data length; this may come into play in decompression and
				// decryption, but we currently have no use for it
				if (Flags[15]) {
					var bytes = Data.Skip(extraHeaderBytes).Take(4);

					var dataLength = (int)ParseUnsignedInteger(bytes);
					Header = Header.Concat(bytes).ToArray();
				}

				Data = Data.Skip(extraHeaderBytes).ToArray();
				ParseData();
			}
		}

		/// <summary>
		/// Fields specific to the ID3v2.4 standard.
		/// </summary>
		public class FormatFields {
			//TODO: RVA2, EQU2, SIGN, SEEK, ASPI

			/// <summary>
			/// A wrapper around an arbitrary <see cref="FieldBase{TVersion}"/>
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
				internal FieldBase<VersionInfo> fieldBase;

				/// <summary>
				/// Initialize the wrapped <see cref="FieldBase{TVersion}"/>
				/// instance.
				/// </summary>
				/// 
				/// <param name="inner">
				/// The underlying implementation to redirect calls to.
				/// </param>
				internal V4FieldWrapper(FieldBase<VersionInfo> inner) =>
					fieldBase = inner;

				/// <summary>
				/// The raw data making up this field's header.
				/// </summary>
				/// 
				/// <seealso cref="Data"/>
				public override byte[] Header {
					get => fieldBase.Header;
					protected set => fieldBase.SetHeader(value);
				}

				/// <summary>
				/// The raw data contained by this field, including any that
				/// would not be displayed by <see cref="Values"/>.
				/// </summary>
				/// 
				/// <seealso cref="Header"/>
				/// <seealso cref="Values"/>
				/// <seealso cref="HasHiddenData"/>
				public override byte[] Data {
					get => fieldBase.Data;
					protected set => fieldBase.SetData(value);
				}

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
				/// The length in bytes of the data contained in the field
				/// (excluding the header).
				/// </summary>
				public override int Length =>
					fieldBase.Length;

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <seealso cref="Data"/>
				/// <see cref="HasHiddenData"/>
				public override IEnumerable<object> Values =>
					fieldBase.Values;

				/// <summary>
				/// Indicates whether this field includes data not displayed
				/// by <see cref="Values"/>.
				/// </summary>
				public override bool HasHiddenData =>
					fieldBase.HasHiddenData;

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				protected override void ParseData() =>
					fieldBase.ParseData();
			}

			/// <summary>
			/// An identifier unique to a particular database.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Owner identifier       (text string) $00
			/// Identifier             (up to 64 bytes binary data)
			/// </c>
			/// </remarks>
			[TagField("UFID")]
			public class UniqueFileId : V4FieldWrapper {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public UniqueFileId(byte[] header)
					: base(new UniqueFileIdBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// Any of the many tags containing purely textual data.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Text encoding          $xx
			/// Information            (text string according to encoding)
			/// </c>
			/// </remarks>
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
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public TextFrame(byte[] header)
					: this(header, null) { }

				/// <summary>
				/// The constructor required to properly initialize the inner
				/// implementation of the
				/// <see cref="V4FieldWrapper"/>.
				/// <para/>
				/// Used when the inheriting field does not exist in ID3v2.3.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				/// <param name="defaultName">
				/// The name to use if no more specific one is found, or
				/// <c>null</c> to use the default name as specified in the
				/// resources.
				/// </param>
				/// <param name="resources">
				/// The resources to use when looking up dynamic strings, or
				/// <c>null</c> to use the default
				/// <see cref="Strings.ID3v23Plus.ResourceManager"/>.
				/// </param>
				internal TextFrame(byte[] header, FieldBase<VersionInfo>.ResourceAccessor defaultName, System.Resources.ResourceManager resources = null)
					: base(new TextFrameBase<VersionInfo>(header, defaultName, resources)) { }

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
				internal TextFrame(TextFrameBase<VersionInfo> inner) : base(inner) { }

				/// <summary>
				/// All strings contained within this field, still unboxed.
				/// </summary>
				protected IEnumerable<string> StringValues =>
					(fieldBase as TextFrameBase<VersionInfo>)?.StringValues;
			}

			/// <summary>
			/// A frame containing a number that may optionally be followed
			/// by a total count (eg. "Track 5 of 13").
			/// </summary>
			[TagField("TPOS")]
			[TagField("TRCK")]
			public class OfNumberFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public OfNumberFrame(byte[] header)
					: base(new OfNumberFrameBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// A frame containing the ISRC of the recording.
			/// </summary>
			[TagField("TSRC")]
			public class IsrcFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public IsrcFrame(byte[] header)
					: base(new IsrcFrameBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// A frame containing a mapping of role to person, or similar.
			/// </summary>
			[TagField("TIPL")]
			[TagField("TMCL")]
			public class ListMappingFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public ListMappingFrame(byte[] header)
					: base(new ListMappingFrameBase<VersionInfo>(header)) { }
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
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public MsFrame(byte[] header)
					: base(new MsFrameBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// A frame containing the musical key.
			/// </summary>
			[TagField("TKEY")]
			public class KeyFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public KeyFrame(byte[] header)
					: base(new KeyFrameBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// A frame containing the language(s) sung/spoken.
			/// </summary>
			[TagField("TLAN")]
			public class LanguageFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public LanguageFrame(byte[] header)
					: base(new LanguageFrameBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// A frame containing the genre.
			/// </summary>
			/// 
			/// <remarks>
			/// While similar to the ID3v2.3 tag, the syntax is sufficiently
			/// different to make it not worth sharing code. The resources,
			/// however, may still be shared.
			/// </remarks>
			[TagField("TCON")]
			public class GenreFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public GenreFrame(byte[] header)
					: base(header) { }

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
								yield return Strings.ID3v23Plus.Field_TCON_RX;
							else if (s.Equals("CR"))
								yield return Strings.ID3v23Plus.Field_TCON_CR;
							else if (s.All(char.IsDigit) && (s.Length <= 3)) {
								/* Given that everything is a digit and the
								 * length is capped, `Parse` is guaranteed to
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
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public ResourceFrame(byte[] header)
					: base(new ResourceFrameBase<VersionInfo>(header)) { }
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
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public ResourceValueFrame(byte[] header)
					: base(new ResourceValueFrameBase<VersionInfo>(header)) { }
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
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public TimeFrame(byte[] header)
					: base(new TimeFrameBase<VersionInfo>(header, (() => Strings.ID3v23Plus.Field_DefaultName_Time), Strings.ID3v24.ResourceManager)) { }
			}

			/// <summary>
			/// A frame containing encoder-defined text.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Text encoding          $xx
			/// Description            (text string according to encoding) $00 [00]
			/// Value                  (text string according to encoding)
			/// </c>
			/// </remarks>
			[TagField("TXXX")]
			public class UserTextFrame : TextFrame {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public UserTextFrame(byte[] header)
					: base(new UserTextFrameBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// Any frame containing a URL.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// URL                    (text string)
			/// </c>
			/// </remarks>
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
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public UrlFrame(byte[] header)
					: base(new UrlFrameBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// A frame containing an encoder-defined URL.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Text encoding          $xx
			/// Description            (text string according to encoding) $00 [00]
			/// URL                    (text string)
			/// </c>
			/// </remarks>
			[TagField("WXXX")]
			public class UserUrlFrame : V4FieldWrapper {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public UserUrlFrame(byte[] header)
					: base(new UserUrlFrameBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// A frame containing text with a language, a description, and
			/// non-null-separated text.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Text encoding          $xx
			/// Language               $xx xx xx
			/// Content descriptor     (text string according to encoding) $00 [00]
			/// Lyrics/text            (full text string according to encoding)
			/// </c>
			/// </remarks>
			[TagField("COMM")]
			[TagField("USLT")]
			public class LongTextFrame : V4FieldWrapper {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public LongTextFrame(byte[] header)
					: base(new LongTextFrameBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// An embedded image.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Text encoding          $xx
			/// MIME type              (text string) $00
			/// Picture type           $xx
			/// Description            (text string according to encoding) $00 [00]
			/// Picture data           (binary data)
			/// </c>
			/// </remarks>
			[TagField("APIC")]
			public class PictureField : V4FieldWrapper {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public PictureField(byte[] header)
					: base(new PictureFieldBase<VersionInfo>(header)) { }
			}

			/// <summary>
			/// A frame containing a single binary counter.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Counter                $xx xx xx xx [xx ...]
			/// </c>
			/// </remarks>
			[TagField("PCNT")]
			public class CountFrame : V4FieldWrapper {
				/// <summary>
				/// The constructor required by
				/// <see cref="ID3v23Plus.V3PlusField{TVersion}.Initialize(IEnumerable{byte})"/>.
				/// <para/>
				/// This should not be called manually.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				public CountFrame(byte[] header)
					: base(new CountFrameBase<VersionInfo>(header)) { }
			}
		}
	}
}
