/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.Collections.Generic;
using System.Linq;

namespace AgEitilt.CardCatalog.Audio.ID3v2 {
	/// <summary>
	/// An implementation of the ID3v2.2 standard as described at
	/// <see href="http://id3.org/id3v2-00"/>
	/// </summary>
	[MetadataFormat(format)]
	public partial class V2 : ID3v2 {
		/// <summary>
		/// The short name used to represent ID3v2.2 metadata.
		/// </summary>
		/// 
		/// <seealso cref="FormatRegistry.RegisterFormat{T}(string)"/>
		public const string format = "ID3v2.2";
		/// <summary>
		/// The display name of the tag format.
		/// </summary>
		public override string Format => format;

		/// <summary>
		/// Check whether the stream begins with a valid ID3v2.2 header.
		/// </summary>
		/// 
		/// <param name="header">The sequence of bytes to check.</param>
		/// 
		/// <returns>
		/// An empty <see cref="V2"/> object if the header is in the proper
		/// format, `null` otherwise.
		/// </returns>
		[HeaderParser(10)]
		public static V2 VerifyHeader(IEnumerable<byte> header) {
			if ((VerifyBaseHeader(header)?.Equals(0x02) ?? false) == false)
				return null;
			else
				return new V2(header);
		}

		/// <summary>
		/// Implement the audio field attribute mappings for ID3v2.2 tags.
		/// </summary>
		class AttributeStruct : AudioTagAttributes {
			private V2 parent;

			public AttributeStruct(V2 parent) {
				this.parent = parent;
			}

			public override IEnumerable<string> Name => null;
		}
		/// <summary>
		/// Retrieve the audio field attribute mappings for ID3v2.2 tags.
		/// </summary>
		/// 
		/// <seealso cref="MetadataTag.Fields"/>
		public override AudioTagAttributes AudioAttributes => new AttributeStruct(this);

		/// <summary>
		/// Indicates whether the data in the tag has been compressed; the
		/// ID3v2.2 spec recommends ignoring the tag if so.
		/// </summary>
		public bool Compressed { get; }

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
		V2(IEnumerable<byte> header) {
			var flags = ParseBaseHeader(header);

			bool useUnsync = flags[0];
			Compressed = flags[1];
			/*TODO: May be better to skip reading the tag rather than setting
             * FlagUnknown, as these flags tend to be critical to the proper
             * parsing of the tag.
             */
			FlagUnknown = (flags.Cast<bool>().Skip(2).Contains(true));
		}
	}
}