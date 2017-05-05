﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;

using AgEitilt.Common.Stream.Extensions;

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
			/// <param name="header">The header to parse.</param>
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
			/// <summary>
			/// Variables specific to the version of ID3v2 implemented.
			/// </summary>
			static protected TVersion version = new TVersion();

			/// <summary>
			/// Reduce the lookups of field types by caching the return.
			/// </summary>
			static IReadOnlyDictionary<byte[], Type> fields = FormatRegistry.FieldTypes(version.FormatName);

			/// <summary>
			/// Valid ID3v2 field identification characters.
			/// </summary>
			protected static char[] HeaderChars => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

			/// <summary>
			/// A byte sequence indicating that the "header" read is actually
			/// padding rather than data.
			/// </summary>
			static byte[] padding = new byte[4] { 0x00, 0x00, 0x00, 0x00 };

			internal static int LengthFromHeader(byte[] header) =>
				(int)ParseUnsignedInteger(header.Skip(4).Take(4).ToArray(), version.FieldSizeBits);

			/// <summary>
			/// Check whether the stream begins with a valid field header.
			/// </summary>
			/// 
			/// <param name="header">The binary header to check.</param>
			/// 
			/// <returns>
			/// An empty <see cref="TagField"/> object if the header is in the
			/// proper format, `null` otherwise.
			/// </returns>
			[HeaderParser(10)]
			public static TagField Initialize(IEnumerable<byte> header) {
				var name = header.Take(4).ToArray();
				var headerArray = header.ToArray();

				if (name.SequenceEqual(padding)) {
					//TODO: Implement official padding handling rather than
					// just ignoring it
					return null;
				} else if (fields.ContainsKey(name) == false) {
					return new TagField.Empty(headerArray, name, LengthFromHeader(headerArray));
				}

				var field = Activator.CreateInstance(fields[name], new object[1] { header.ToArray() });
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

			/// <summary>
			/// The flags set on the field.
			/// </summary>
			protected BitArray Flags { get; set; }

			/// <summary>
			/// Whether the header includes a non-standard flag, which may
			/// result in unrecognizable data.
			/// </summary>
			/// 
			/// <remarks>
			/// TODO: Store data about the unknown flags rather than simply
			/// indicating their presence.
			/// </remarks>
			protected bool FlagUnknown { get; set; }

			/// <summary>
			/// The number of data bytes associated with flags in the header.
			/// </summary>
			/// 
			/// <remarks>
			/// These are included in the tag length value in the header, but
			/// are associated with the header rather than the data in this
			/// library.
			/// </remarks>
			protected int extraHeaderBytes = 0;

			/// <summary>
			/// Perform field-specific parsing after the required common
			/// parsing has been handled.
			/// </summary>
			/// 
			/// <remarks>
			/// This will typically be used to cache expensive computations or
			/// to set a field for <see cref="TagField.HasHiddenData"/>.
			/// </remarks>
			protected virtual void ParseData() { }
		}

		/// <summary>
		/// Wrapper to organize the classes used to share field content
		/// implementations between both the ID3v2.3 and ID3v2.4 standards.
		/// </summary>
		/// 
		/// <remarks>
		/// This may include fields that aren't specified by the ID3v2.3
		/// standards, on the basis that it is better to recognize fields that
		/// have been backported from ID3v2.4. Note that, in these cases, the
		/// experimental flag should probably be set.
		/// </remarks>
		public static class FormatFieldBases {
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

			/*TODO: MCDI, ETCO, MLLT, SYTC, SYLT, RVRB, GEOB, POPM, RBUF,
			 * AENC, LINK, POSS, USER, OWNE, COMR, ENCR, GRID, PRIV
			 * 
			 * Unofficial (most may never have a need for inclusion):
			 * GRP1, MVNM, MVIN, PCST, TSIZ, MCDI, ITNU, XDOR, XOLY
			 */

			/// <summary>
			/// An abstract base for handling common field processing tasks.
			/// </summary>
			internal abstract class FieldBase<TVersion> : TagField where TVersion : VersionInfo, new() {
				/// <summary>
				/// Variables specific to the version of ID3v2 implemented.
				/// </summary>
				static protected TVersion version = new TVersion();

				/// <summary>
				/// Convert a ID3v2 byte representation of an encoding into the
				/// proper <see cref="Encoding"/> object.
				/// </summary>
				/// 
				/// <remarks>
				/// While the ID3v2.3 specification only lists the first two
				/// potential encodings, it is otherwise compatible with the
				/// full list from ID3v2.4, and so using this for both allows
				/// slightly more error tolerance.
				/// </remarks>
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
						case 0x01:
						default:
							return null;
						case 0x02:
							return Encoding.BigEndianUnicode;
						case 0x03:
							return Encoding.UTF8;
					}
				}

				/// <summary>
				/// The constructor required to initialize the field.
				/// </summary>
				/// 
				/// <param name="header">The binary header to parse.</param>
				/// <param name="defaultName">
				/// The name to use if no more specific one is found, or
				/// <c>null</c> to use the fallback <see cref="TagField.Name"/>.
				/// </param>
				/// <param name="resources">
				/// The resources to use when looking up dynamic strings, or
				/// <c>null</c> to use the default
				/// <see cref="Strings.ID3v23Plus.ResourceManager"/>.
				/// </param>
				public FieldBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null) {
					Header = header;
					// Ensure that the delegate will always be assigned some
					// callable function
					DefaultName = defaultName ?? (() => null);
					Resources = resources ?? Strings.ID3v23Plus.ResourceManager;
				}

				/// <summary>
				/// Provide a public interface for setting the header, as this
				/// class is meant to be used within a wrapper.
				/// </summary>
				/// 
				/// <seealso cref="TagField.Header"/>
				public void SetHeader(byte[] header) =>
					Header = header;

				/// <summary>
				/// Provide a public interface for setting the data, as this
				/// class is meant to be used within a wrapper.
				/// </summary>
				/// 
				/// <seealso cref="TagField.Data"/>
				public void SetData(byte[] data) =>
					Data = data;

				/// <summary>
				/// The byte header used to internally identify the field.
				/// </summary>
				public override byte[] SystemName =>
					Header.Take(4).ToArray();

				/// <summary>
				/// The version-specific resources to use for string lookups.
				/// </summary>
				protected ResourceManager Resources { get; }

				/// <summary>
				/// The human-readable name of the field.
				/// </summary>
				public override string Name =>
					/* Recognized nonstandard fields/usage:
					 * TCAT: "Category"               (from podcasts)
					 * TDES: "Description"            (from podcasts)
					 * TGID: "Album ID"               (podcasts: typically "Podcast ID")
					 * TIT1: "Work"                   (officially "Grouping")
					 * TKWD: "Keywords"               (from podcasts)
					 * TOAL: "Original alubm"         (Picard: "Work title")
					 * TPUB: "Publisher"              (Picard: "Record label")
					 * TSO2: "Album artist sort order"
					 * TSOC: "Composer sort order"
					 * WFED: "Feed"                   (from podcasts)
					 * XSOA: "Album sort order (alternate)"
					 * XSOP: "Artist sort order (alternate)"
					 * XSOT: "Title sort order (alternate)"
					 */
					Resources.GetString("Field_" + ISO88591.GetString(SystemName))
						?? Strings.ID3v23Plus.ResourceManager.GetString("Field_" + ISO88591.GetString(SystemName))
						?? DefaultName()
						?? base.Name;

				/// <summary>
				/// Delay a call to the reource files to ensure the correctly
				/// localized version is used, if locale changes during run.
				/// </summary>
				/// 
				/// <returns>The localized string resource.</returns>
				public delegate string ResourceAccessor();
				/// <summary>
				/// The name to use if the header was not matched.
				/// </summary>
				ResourceAccessor DefaultName;

				/// <summary>
				/// Extra human-readable information describing the field, such as the
				/// "category" of a header with multiple realizations.
				/// </summary>
				public override string Subtitle {
					get {
						if (Name.Equals(DefaultName()))
							return null;
						else
							return base.Subtitle;
					}
				}

				public override int Length =>
					V3PlusField<TVersion>.LengthFromHeader(Header);

				/// <summary>
				/// Perform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				/// 
				/// <remarks>
				/// This will typically be used to cache expensive computations or
				/// to set a field for <see cref="TagField.HasHiddenData"/>.
				/// </remarks>
				public virtual void ParseData() { }
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
			internal class UniqueFileIdBase<TVersion> : FieldBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public UniqueFileIdBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName:defaultName, resources:resources) { }

				/// <summary>
				/// The database with which this ID is associated.
				/// </summary>
				string owner = null;
				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle =>
					owner;

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
				public override void ParseData() {
					owner = ISO88591.GetString(Data.TakeWhile(b => b != 0x00).ToArray());
					id = Data.Skip(owner.Length + 1).ToArray();
				}
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
			internal class TextFrameBase<TVersion> : FieldBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public TextFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, (defaultName ?? (() => Strings.ID3v23Plus.Field_DefaultName_Text)), resources) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values =>
					StringValues;
				/// <summary>
				/// All strings contained within this field.
				/// </summary>
				public IEnumerable<string> StringValues { get; private set; }

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				public override void ParseData() {
					// SplitStrings doesn't care about length, but shouldn't
					// be passed the unset tail if the stream ended early
					StringValues = SplitStrings(Data.Skip(1).ToArray(), TryGetEncoding(Data.First()));
				}

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
				/// <param name="count">
				/// The maximum number of substrings to return, as in
				/// <see cref="string.Split(char[], int)"/>.
				/// </param>
				/// 
				/// <returns>The separated and parsed strings.</returns>
				internal static IEnumerable<string> SplitStrings(byte[] data, Encoding encoding, int count = int.MaxValue) {
					var raw = (encoding == null ? ReadFromByteOrderMark(data) : encoding.GetString(data));
					var split = raw.Split(new char[1] { '\0' }, count, StringSplitOptions.None);

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
			}

			/// <summary>
			/// A frame containing a number that may optionally be followed by
			/// a total count (eg. "Track 5 of 13").
			/// </summary>
			internal class OfNumberFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public OfNumberFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, (defaultName ?? (() => Strings.ID3v23Plus.Field_DefaultName_Number)), resources) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						return StringValues.Select<string, object>(s => {
							var split = s?.Split(new char[1] { '/' }, 2) ?? Array.Empty<string>();

							// If there's nothing in the array, this value
							// shouldn't be returned
							if (split.Length <= 0) {
								return null;

								// If the value is only a single number...
							} else if (split.Length == 1) {
								// ...try returning an int to be more specific...
								if (int.TryParse(split[0], out var parsed))
									return parsed;
								// ...but fall back on the original string
								else
									return s;

								// If the value seems to be split in two...
							} else {
								// ...only format if it seems to be "# of total"
								// (note the max count on the .Split(...) call)...
								if (int.TryParse(split[0], out var parsed0) && int.TryParse(split[1], out var parsed1))
									return String.Format(Strings.ID3v23Plus.Field_ValueFormat_Number, parsed0, parsed1);
								// ...and return the original if it's more complex
								else
									return s;
							}

							// Filter out any values we've said to remove
						}).Where(s => s != null);
					}
				}
			}

			/// <summary>
			/// A frame containing the ISRC of the recording.
			/// </summary>
			internal class IsrcFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public IsrcFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, (defaultName ?? (() => Strings.ID3v23Plus.Field_DefaultName_Length)), resources) { }


				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values =>
					from isrc in StringValues
					where isrc.Contains('-') == false
					where isrc.Length == 12
					select isrc.Insert(2, "-")
						.Insert(6, "-")
						.Insert(9, "-");
			}

			/// <summary>
			/// A frame containing a mapping of role to person, or similar.
			/// </summary>
			/// 
			/// <remarks>
			/// TODO: This is a good candidate for allowing multiple subtitles
			/// in some form.
			/// </remarks>
			internal class ListMappingFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public ListMappingFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, (defaultName ?? (() => Strings.ID3v23Plus.Field_DefaultName_Length)), resources) { }


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
			internal class MsFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public MsFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName, resources) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						foreach (var s in StringValues) {
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
			internal class KeyFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public KeyFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName, resources) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						foreach (var s in StringValues) {
							if (s == "o") {
								yield return Strings.ID3v23Plus.Field_TKEY_OffKey;
							} else {
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
			}

			/// <summary>
			/// A frame containing the language(s) sung/spoken.
			/// </summary>
			internal class LanguageFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public LanguageFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName, resources) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Needs better ISO 639-2 lookup ability before
				/// implementation: see solution at
				/// http://stackoverflow.com/questions/12485626/replacement-for-cultureinfo-getcultures-in-net-windows-store-apps
				/// Might also be nice to add e.g. ISO 639-3 support in the
				/// same package ("CultureExtensions").
				/// </remarks>
				public override IEnumerable<object> Values => base.Values;
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
			internal class ResourceFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public ResourceFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName, resources) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values =>
					from keyBase in StringValues
						// Replace non-key-safe characters
					select (keyBase, keyBase.Replace('/', '_').Replace('.', '_')) into keyValues
					// Compose the value into the proper format for lookup
					select Resources.GetString("Field_" + ISO88591.GetString(SystemName) + "_" + keyValues.Item2)
						// Fallback if key not found
						?? String.Format(CardCatalog.Strings.Base.Field_DefaultValue, keyValues.Item1);
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
			internal class ResourceValueFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public ResourceValueFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName, resources) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values =>
					from valueBase in StringValues
						// Try to find the format string for this field
					let format = Resources.GetString("Field_" + ISO88591.GetString(SystemName) + "_Value")
						// Fallback if key not found
						?? CardCatalog.Strings.Base.Field_DefaultValue
					// Apply the value to the format string
					select String.Format(format, valueBase);
			}

			/// <summary>
			/// A frame containing a timestamp.
			/// </summary>
			internal class TimeFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public TimeFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName, resources) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						foreach (var s in StringValues) {
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
								yield return Strings.ID3v23Plus.Field_Time_Unknown;
							else if ((times[1] != null) && (times[1] != DateTimeOffset.MinValue))
								yield return String.Format(Strings.ID3v23Plus.Field_Time_Range, times[0], times[1]);
							else
								//TODO: Only return the given values
								yield return String.Format(Strings.ID3v23Plus.Field_Time_Single, times[0]);
						}
					}
				}
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
			internal class UserTextFrameBase<TVersion> : TextFrameBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public UserTextFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName, resources) { }

				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle =>
					StringValues.FirstOrDefault();

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values =>
					StringValues.Skip(1);
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
			internal class UrlFrameBase<TVersion> : FieldBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public UrlFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName:(defaultName ?? (() => Strings.ID3v23Plus.Field_DefaultName_Url)), resources:resources) { }

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						if ((url == null) || (url.Length == 0))
							return null;
						else
							return new object[] { url };
					}
				}
				/// <summary>
				/// The URL contained within this field.
				/// </summary>
				protected string url = null;

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				public override void ParseData() {
					// Discard everything after the first null
					url = TextFrameBase<TVersion>.SplitStrings(Data, ISO88591, 2).FirstOrDefault();
				}
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
			internal class UserUrlFrameBase<TVersion> : FieldBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public UserUrlFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, (defaultName ?? (() => Strings.ID3v23Plus.Field_DefaultName_Url)), resources) { }

				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle {
					get {
						if ((description == null) || (description.Length == 0))
							return null;
						else
							return description;
					}
				}
				/// <summary>
				/// The underlying, editable subtitle.
				/// </summary>
				string description = null;

				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						if ((url == null) || (url.Length == 0))
							return null;
						else
							return new object[] { url };
					}
				}
				/// <summary>
				/// The URL contained within this field.
				/// </summary>
				protected string url = null;

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				public override void ParseData() {
					// Get the encoding of the description
					var encoding = TryGetEncoding(Data[0]);

					// Retrieve the description according to its encoding
					var split = TextFrameBase<TVersion>.SplitStrings(Data.Skip(1).ToArray(), encoding, 2);
					description = split.FirstOrDefault();

					// Get the URL in ISO-8859-1 from the end of the string
					var remainder = encoding.GetBytes(split.ElementAtOrDefault(1));
					url = TextFrameBase<TVersion>.SplitStrings(remainder, ISO88591, 2).FirstOrDefault();
				}
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
			internal class LongTextFrameBase<TVersion> : FieldBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public LongTextFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, (defaultName ?? (() => Strings.ID3v23Plus.Field_DefaultName_Url)), resources) { }

				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle {
					get {
						bool desc = ((description != null) && (description.Length > 0));
						bool lang = ((description != null) && (description.Length > 0));

						if (desc && lang)
							return String.Format(Strings.ID3v23Plus.Field_Subtitle_Language, description, language);
						else if (desc)
							return description;
						else if (lang)
							return language;
						else
							return null;
					}
				}
				/// <summary>
				/// The underlying, editable subtitle.
				/// </summary>
				string description = null;
				/// <summary>
				/// The language in which the lyrics are
				/// transcribed/translated.
				/// </summary>
				/// 
				/// <remarks>
				/// TODO: Replace with <see cref="CultureInfo"/> object.
				/// <para/>
				/// TODO: Needs better ISO 639-2 lookup ability: see solution
				/// at http://stackoverflow.com/questions/12485626/replacement-for-cultureinfo-getcultures-in-net-windows-store-apps
				/// Might also be nice to add e.g. ISO 639-3 support in the
				/// same package ("CultureExtensions").
				/// </remarks>
				string language = null;

				/// <summary>
				/// The text contained within this frame.
				/// </summary>
				string text = null;
				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values {
					get {
						if ((text == null) || (text.Length == 0))
							return Array.Empty<object>();
						else
							return new object[1] { text };
					}
				}

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				public override void ParseData() {
					// Get the encoding of the description
					var encoding = TryGetEncoding(Data[0]);

					// Get the language code using the invariable encoding
					language = ISO88591.GetString(Data.ToList().GetRange(1, 3).ToArray());

					// Retrieve the other contents according to their encoding
					var split = TextFrameBase<TVersion>.SplitStrings(Data.Skip(4).ToArray(), encoding, 2);
					description = split.FirstOrDefault();
					text = split.ElementAtOrDefault(1);
				}
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
			internal class PictureFieldBase<TVersion> : FieldBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public PictureFieldBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName, resources) { }

				/// <summary>
				/// What is depicted by the image.
				/// </summary>
				ImageCategory category;
				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Name =>
					category.PrintableName() ?? base.Name;

				/// <summary>
				/// The database with which this ID is associated.
				/// </summary>
				string description = null;
				/// <summary>
				/// The description of the contained values.
				/// </summary>
				public override string Subtitle {
					get {
						if ((description == null) || (description.Length == 0))
							return null;
						else
							return description;
					}
				}

				/// <summary>
				/// The raw image data.
				/// </summary>
				ImageData image;
				/// <summary>
				/// All values contained within this field.
				/// </summary>
				public override IEnumerable<object> Values =>
					new object[1] { image };

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				public override void ParseData() {
					Encoding encoding = TryGetEncoding(Data[0]);
					var mime = ISO88591.GetString(Data.Skip(1).TakeWhile(b => b > 0x00).ToArray());
					var category = (ImageCategory)Data[mime.Length + 2];

					var prevZero = false;
					var descriptionArray = Data.Skip(mime.Length + 3).TakeWhile(b => {
						if (b == 0x00) {
							if (encoding == ISO88591)
								return false;
							else
								prevZero = true;
						} else {
							if (prevZero)
								return false;
							else
								prevZero = false;
						}
						return true;
					}).ToArray();
					if (encoding == null)
						description = TextFrameBase<TVersion>.ReadFromByteOrderMark(descriptionArray);
					else
						description = encoding?.GetString(descriptionArray);

					image = new ImageData(Data.Skip(mime.Length + 3 + descriptionArray.Length + (encoding == ISO88591 ? 1 : 2)).ToArray(), mime);
				}
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
			internal class CountFrameBase<TVersion> : FieldBase<TVersion> where TVersion : VersionInfo, new() {
				/// <summary>
				/// The constructor required to initialize the field.
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
				public CountFrameBase(byte[] header, ResourceAccessor defaultName = null, ResourceManager resources = null)
					: base(header, defaultName:defaultName, resources:resources) { }

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
				public override IEnumerable<object> Values =>
					new object[1] { count };

				/// <summary>
				/// Preform field-specific parsing after the required common
				/// parsing has been handled.
				/// </summary>
				public override void ParseData() =>
					count = ParseUnsignedInteger(Data);
			}
		}
	}
}