using System;
using System.Collections;
using System.IO;
using System.Linq;

namespace Metadata.Audio.ID3v2 {
	/// <summary>
	/// An implementation of the ID3v2.3 standard as described at
	/// <see href="http://id3.org/d3v2.3.0"/>
	/// </summary>
	[MetadataFormat(format)]
	public partial class V3 : ID3v23Plus {
		/// <summary>
		/// The short name used to represent ID3v2.3 metadata.
		/// </summary>
		/// 
		/// <seealso cref="MetadataFormat.Register(string, System.Type)"/>
		public const string format = "ID3v2.3";
		/// <summary>
		/// The display name of the tag format.
		/// </summary>
		public override string Format => format;

		/// <summary>
		/// Describe the behaviour of the extended header.
		/// </summary>
		public override ExtendedHeaderProps ExtendedHeader => new ExtendedHeaderProps {
			sizeIncludesItself = false,
			bitsInSize = 8
		};

		/// <summary>
		/// Check whether the stream begins with a valid ID3v2.3 header.
		/// </summary>
		/// 
		/// <param name="header">The sequence of bytes to check.</param>
		/// 
		/// <returns>
		/// An empty <see cref="V2"/> object if the header is in the proper
		/// format, `null` otherwise.
		/// </returns>
		[HeaderParser(10)]
		public static V3 VerifyHeader(byte[] header) {
			if ((VerifyBaseHeader(header)?.Equals(0x03) ?? false) == false)
				return null;
			else
				return new V3(header);
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
		/// Implement the audio field attribute mappings for ID3v2.3 tags.
		/// </summary>
		class AttributeStruct : AudioTagAttributes {
			private V3 parent;

			public AttributeStruct(V3 parent) {
				this.parent = parent;
			}

			public override AttributeValues Name => throw new NotImplementedException();
		}
		/// <summary>
		/// Retrieve the audio field attribute mappings for ID3v2.3 tags.
		/// </summary>
		/// 
		/// <seealso cref="Fields"/>
		public override AudioTagAttributes AudioAttributes => new AttributeStruct(this);

		/// <summary>
		/// The size of the empty padding at the end of the tag.
		/// </summary>
		private uint PaddingSize { get; set; }

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
		V3(byte[] header) {
			PaddingSize = 0;

			var flags = ParseBaseHeader(header);

			bool useUnsync = flags[0];
			HasExtendedHeader = flags[1];
			IsExperimental = flags[2];
			/*TODO: May be better to skip reading the tag rather than setting
             * FlagUnknown, as these flags tend to be critical to the proper
             * parsing of the tag.
             */
			FlagUnknown = (flags.Cast<bool>().Skip(3).Contains(true));
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
			if (extHeader.Length < 6)
				throw new InvalidDataException("Extended header too short to be valid for ID3v2.3");

			var flags = new BitArray(extHeader.Take(2).ToArray());
			PaddingSize = ParseInteger(extHeader.ToList().GetRange(2, 4));

			if (flags[0]) {
				if (extHeader.Length < 10)
					throw new InvalidDataException("Extended header too short to contain a valid ID3v2.3 CRC");

				TagCRC = ParseInteger(extHeader.ToList().GetRange(6, 4));
			}
			FlagUnknown = (flags.Cast<bool>().Skip(1).Contains(true));

			//            if (CheckCRCIfPresent(tag.Take((int)(tag.Length - PaddingSize)).ToArray()) == false)
			//                throw new InvalidDataException("ID3 tag does not match saved checksum");
		}
	}
}