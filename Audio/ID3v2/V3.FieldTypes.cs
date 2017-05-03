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
using AgEitilt.Common.Stream.Extensions;
using static AgEitilt.CardCatalog.Audio.ID3v2.ID3v23Plus.FormatFieldBases;

namespace AgEitilt.CardCatalog.Audio.ID3v2 {
	partial class V3 {
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
		public abstract class V3Field : V3PlusField<V3Field.V3VersionInfo> {
			/// <summary>
			/// Behaviours required for field initialization, specific to a
			/// particular version of the ID3v2 standard.
			/// </summary>
			public class V3VersionInfo : VersionInfo {
				/// <summary>
				/// The unique identifier for this version.
				/// </summary>
				public override string FormatName => format;

				/// <summary>
				/// The number of data bits used in the header field size bytes.
				/// </summary>
				public override uint FieldSizeBits => 8;

				/// <summary>
				/// Version-specific code for parsing field headers.
				/// </summary>
				/// 
				/// <param name="fieldObj">
				/// The new, boxed instance of the field.
				/// </param>
				/// <param name="header">The header to parse.</param>
				public override void Initialize(object fieldObj, IEnumerable<byte> header) {
					var field = fieldObj as V3Field;
					if (field == null)
						return;

					// Store this now, but it needs to be parsed when we get the
					// rest of the data
					field.Flags = new BitArray(header.Skip(8).ToArray());
					field.FlagUnknown = field.Flags.And(new BitArray(new byte[2] { 0b00011111, 0b00011111 })).Cast<bool>().Any();
				}
			}

			/// <summary>
			/// Indicates that this field should be removed if the tag is
			/// edited in any way, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public override bool DiscardUnknownOnTagEdit => Flags[0];
			/// <summary>
			/// Indicates that this field should be removed if the file
			/// external to the tag is edited in any way EXCEPT if the audio
			/// is completely replaced, and the program doesn't know how to
			/// compensate.
			/// </summary>
			public override bool DiscardUnknownOnFileEdit => Flags[1];
			/// <summary>
			/// Indicates that this field should not be changed without direct
			/// knowledge of its contents and structure.
			/// </summary>
			public override bool IsReadOnlyIfUnknown => Flags[2];

			/// <summary>
			/// Indicates that the data in the field is compressed using the
			/// zlib compression scheme.
			/// </summary>
			public override bool IsFieldCompressed => Flags[8];

			/// <summary>
			/// Indicates that the data in the field is encrypted according to
			/// a specified method.
			/// </summary>
			public override bool IsFieldEncrypted => Flags[9];

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

				// Grouping
				/* Need to check the flag directly, as the property looks at
				 * `group` instead.
				 */
				if (Flags[10]) {
					int b = stream.ReadByte();
					--Length;

					if (b >= 0)
						group = (byte)b;
				}

				ParseData(stream);
			}
		}

		/// <summary>
		/// Fields specific to the ID3v2.3 standard.
		/// </summary>
		public class FormatFields {
			//TODO: TCON, TDAT, TIME, TORY, TRDA, TSIZ, TYER, RVAD, EQUA

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
			public class V3FieldWrapper : V3Field {
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
				public V3FieldWrapper(FieldBase inner) =>
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

				/// <summary>
				/// Determines whether a field has a title specific to v2.3.
				/// </summary>
				/// 
				/// <remarks>
				/// While some tags were deprecated in ID3v2.4, many of those
				/// are still defined in <c>ID3v23Plus.resx</c> in order to
				/// support tag writers that still used the old fields.
				/// </remarks>
				/// 
				/// <param name="name">
				/// The unique header of the field to check.
				/// </param>
				/// 
				/// <returns>
				/// Whether the field title is defined in <c>ID3v23.resx</c>.
				/// </returns>
				protected static bool IsLocalTitle(byte[] name) {
					switch (ISO88591.GetString(name)) {
						default:
							return false;
					}
				}
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
			public class UniqueFileId : V3FieldWrapper {
				/// <summary>
				/// The constructor required to properly initialize the inner
				/// implementation of the <see cref="V3FieldWrapper"/>.
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
			public class TextFrame : V3FieldWrapper {
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
									case "COP":  // Copyright
									case "DLY":  // Playlist delay
									case "FLT":  // Audio encoding
									case "LAN":  // Language
									case "LEN":  // Length
									case "KEY":  // Key
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
				/// implementation of the <see cref="V3FieldWrapper"/>.
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
				/// <param name="resources">
				/// The resources to use when looking up dynamic strings, or
				/// <c>null</c> to use the default
				/// <see cref="Strings.ID3v23Plus.ResourceManager"/>.
				/// </param>
				public TextFrame(byte[] name, int length, FieldBase.ResourceAccessor defaultName, System.Resources.ResourceManager resources = null)
					: base(new TextFrameBase(name, length, TryGetEncoding, defaultName, (IsLocalTitle(name) ? Strings.ID3v24.ResourceManager : resources))) { }

				/// <summary>
				/// The constructor required to properly initialize the inner
				/// implementation of the <see cref="V3FieldWrapper"/>.
				/// <para/>
				/// Used when the content behaviour is shared with ID3v2.3.
				/// </summary>
				/// 
				/// <param name="inner">
				/// The underlying implementation to redirect calls to.
				/// </param>
				public TextFrame(TextFrameBase inner) : base(inner) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values =>
					// The FieldBase parses according to v2.4 format, but v2.3
					// ignores everything after the first `null`
					fieldBase.Values;

				/// <summary>
				/// All strings contained within this field, still unboxed.
				/// </summary>
				/// 
				/// <remarks>
				/// Note that, as this is intended as internal access to the
				/// raw data, it returns every string in the field as opposed
				/// to <see cref="Values"/>.
				/// </remarks>
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
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// URL                    (text string)
			/// </c>
			/// </remarks>
			[TagField]
			public class UrlFrame : V3FieldWrapper {
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
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Text encoding          $xx
			/// Description            (text string according to encoding) $00 [00]
			/// URL                    (text string)
			/// </c>
			/// </remarks>
			[TagField("WXXX")]
			public class UserUrlFrame : V3FieldWrapper {
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
			/// A frame containing a mapping of role to person, or similar.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Text encoding          $xx
			/// Information            (text strings according to encoding, null-separated)
			/// </c>
			/// </remarks>
			[TagField("IPLS")]
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
					: base(new ListMappingFrameBase(name, length, TryGetEncoding)) { }
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
			public class LongTextFrame : V3FieldWrapper {
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
			public class PictureField : V3FieldWrapper {
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
			/// A frame containing a single binary counter.
			/// </summary>
			/// 
			/// <remarks>
			/// Data is in the format:<c>
			/// Counter                $xx xx xx xx [xx ...]
			/// </c>
			/// </remarks>
			[TagField("PCNT")]
			public class CountFrame : V3FieldWrapper {
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
