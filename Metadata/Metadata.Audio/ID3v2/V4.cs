using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Metadata.Audio.ID3v2 {
	/// <summary>
	/// An implementation of the ID3v2.4 standard as described at
	/// <see href="http://id3.org/id3v2.4.0-structure"/> and
	/// <see href="http://id3.org/id3v2.4.0-frames"/>
	/// </summary>
	/// 
	/// <remarks>
	/// TODO: Handle footer
	/// </remarks>
	[MetadataFormat(format)]
	public partial class V4 : ID3v23Plus {
		/// <summary>
		/// The short name used to represent ID3v2.4 metadata.
		/// </summary>
		/// 
		/// <seealso cref="MetadataFormat.Register{T}(string)"/>
		public const string format = "ID3v2.4";
		/// <summary>
		/// The display name of the tag format.
		/// </summary>
		public override string Format => format;

		/// <summary>
		/// Describe the behaviour of the extended header.
		/// </summary>
		public override ExtendedHeaderProps ExtendedHeader => new ExtendedHeaderProps {
			sizeIncludesItself = true,
			bitsInSize = 7
		};

		/// <summary>
		/// Check whether the stream begins with a valid ID3v2.4 header.
		/// </summary>
		/// 
		/// <param name="header">The sequence of bytes to check.</param>
		/// 
		/// <returns>
		/// An empty <see cref="V2"/> object if the header is in the proper
		/// format, `null` otherwise.
		/// </returns>
		[HeaderParser(10)]
		public static V4 VerifyHeader(byte[] header) {
			if ((VerifyBaseHeader(header)?.Equals(0x04) ?? false) == false)
				return null;
			else
				return new V4(header);
		}

		/// <summary>
		/// The underlying low-level tag data.
		/// </summary>
		/// 
		/// <seealso cref="Fields"/>
		private FieldDictionary fields = new FieldDictionary();
		/// <summary>
		/// The low-level representations of the tag data.
		/// </summary>
		public override IReadOnlyFieldDictionary Fields => fields;

		/// <summary>
		/// Implement the audio field attribute mappings for ID3v2.4 tags.
		/// </summary>
		class AttributeStruct : AudioTagAttributes {
			private V4 parent;

			public AttributeStruct(V4 parent) {
				this.parent = parent;
			}

			public override IEnumerable<string> Name => parent.Fields[ISO88591.GetBytes("TIT2")].SelectMany(f => f.Values);
		}
		/// <summary>
		/// Retrieve the audio field attribute mappings for ID3v2.4 tags.
		/// </summary>
		/// 
		/// <seealso cref="Fields"/>
		public override AudioTagAttributes AudioAttributes => new AttributeStruct(this);

		/// <summary>
		/// Whether the tag is closed with a footer.
		/// </summary>
		bool HasFooter { get; set; }

		/// <summary>
		/// Whether this tag updates any previous tags
		/// </summary>
		bool TagIsUpdate { get; set; }

		/// <summary>
		/// Parse a stream according the proper version of the ID3v2
		/// specification, from the current location.
		/// </summary>
		/// 
		/// <remarks>
		/// As according to the recommendation in the ID3v2.2 specification,
		/// if the tag is compressed, it is swallowed but largely ignored.
		/// </remarks>
		/// 
		/// <param name="header">The stream to parse.</param>
		V4(byte[] header) {
			var flags = ParseBaseHeader(header);

			bool useUnsync = flags[0];
			HasExtendedHeader = flags[1];
			IsExperimental = flags[2];
			HasFooter = flags[3];
			/*TODO: May be better to skip reading the tag rather than setting
             * FlagUnknown, as these flags tend to be critical to the proper
             * parsing of the tag.
             */
			FlagUnknown = (flags.Cast<bool>().Skip(4).Contains(true));

		}

		/// <summary>
		/// Extract and encapsulate the code used to parse a ID3v2 extended
		/// header into usable variables.
		/// <para/>
		/// Given that arrays have an inherent Length property, the first four
		/// bytes (storing the size) are ignored.
		/// </summary>
		/// 
		/// <param name="extHeader">
		/// The de-unsynchronized byte array to parse.
		/// </param>
		protected override void ParseExtendedHeader(byte[] extHeader) {
			//TODO: Doesn't ensure that the length is enough for the flag data
			// and so array index out-of-bounds exceptions may be thrown.
			if (extHeader.Length < 2)
				throw new InvalidDataException("Extended header too short to be valid for ID3v2.4");

			int flagBytes = (int)ParseInteger(new byte[1]{ extHeader[0] });
			var flags = new BitArray(extHeader.ToList().GetRange(1, flagBytes).ToArray());

			int pos = flagBytes + 1;

			// Shouldn't be set according to the standard, but non-traditional
			// encoders may have assigned some meaning to it
			if (flags[0]) {
				FlagUnknown = true;
				pos += extHeader[pos] + 1;
			}
			if (flags[1]) {
				if (extHeader[pos] != 0x00)
					throw new InvalidDataException("Invalid length (" + extHeader[pos] + ") given for ID3v2.4 'Tag is an update' data");
				TagIsUpdate = true;
				++pos;
			}
			if (flags[2]) {
				if (extHeader[pos] != 0x05)
					throw new InvalidDataException("Invalid length (" + extHeader[pos] + ") given for ID3v2.4 'CRC data present' data");
				TagCRC = ParseInteger(extHeader.ToList().GetRange(pos + 1, 5), 7);
				pos += 6;
			}
			if (flags[3]) {
				if (extHeader[pos] != 0x01)
					throw new InvalidDataException("Invalid length (" + extHeader[pos] + ") given for ID3v2.4 'Tag restrictions' data");
				//TODO: This only affects behaviour before encoding, but
				// should still be handled
				++pos;
			}
			FlagUnknown = (flags.Cast<bool>().Skip(4).Contains(true));

			//            if (CheckCRCIfPresent(tag) == false)
			//                throw new InvalidDataException("ID3 tag does not match saved checksum");
		}
	}
}