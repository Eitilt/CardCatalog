/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace AgEitilt.CardCatalog.Audio.ID3v2 {
	/// <summary>
	/// Shared code for ID3v2.3 and later.
	/// </summary>
	public abstract partial class ID3v23Plus : ID3v2 {
		/// <summary>
		/// Minor behaviour dependent on the version of the specification.
		/// </summary>
		public struct ExtendedHeaderProps {
			/// <summary>
			/// Whether the listed size of the extended header includes the
			/// four bytes containing that size.
			/// </summary>
			public bool sizeIncludesItself;
			/// <summary>
			/// The number of content bits per byte used to store the size.
			/// </summary>
			public uint bitsInSize;
		}
		/// <summary>
		/// Minor behaviour dependent on the version of the specification.
		/// </summary>
		public abstract ExtendedHeaderProps ExtendedHeader { get; }

		/// <summary>
		/// Indicates that the tag contains an extended header that needs to
		/// be parsed before any fields.
		/// </summary>
		protected bool HasExtendedHeader { get; set; }

		/// <summary>
		/// Indicates that the tag is in an experimental stage.
		/// </summary>
		/// 
		/// <remarks>Just as ill-defined in the ID3v2 specification.</remarks>
		protected bool IsExperimental { get; set; } = false;
		/// <summary>
		/// The CRC calculated for the data in the tag, or `null` if is was
		/// not (yet) read.
		/// </summary>
		protected uint? TagCRC { get; set; } = null;

		/// <summary>
		/// Parse an ID3v2 extended header starting at the current position in
		/// the stream, while retrieving the remainder of the tag in the
		/// background.
		/// </summary>
		/// 
		/// <param name="stream">The stream to read from.</param>
		/// <param name="tagSize">The total size of the ID3v2 tag.</param>
		/// <param name="useUnsync">
		/// Whether the entire tag has been unsynchronized.
		/// </param>
		/// <param name="extendedHeaderPresent">
		/// Whether the tag contains an extended header.
		/// </param>
		/// 
		/// <returns>
		/// The remainder of the ID3v2 tag, already processed to reverse any
		/// unsynchronization.
		/// </returns>
		protected async Task<byte[]> ReadExtHeaderWithTagAsync(Stream stream, uint tagSize, bool useUnsync, bool extendedHeaderPresent) {
			logger?.LogDebug(Strings.ID3v23Plus.Logger_ParseExtHeader);

			if (extendedHeaderPresent == false)
				return await ReadBytesAsync(stream, tagSize, useUnsync).ConfigureAwait(false);

			uint sizeCount = 4;
			uint extSize = ParseUnsignedInteger(stream, useUnsync, ref sizeCount, ExtendedHeader.bitsInSize);

			// The size data in ID3v2.4 includes the size bytes, which can
			// be disregarded as they've already been read.
			if (ExtendedHeader.sizeIncludesItself)
				extSize -= 4;

			byte[] extHeader = GetUnsyncronizedBytes(stream, ref extSize);

			// Start reading (and processing if necessary) from the stream
			// in the background to save a small bit of waiting while
			// parsing the extended header
			var readTask = ReadBytesAsync(stream, (tagSize - sizeCount - extSize), useUnsync);

			ParseExtendedHeader(extHeader);

			return await readTask.ConfigureAwait(false);
		}

		/// <summary>
		/// Extract and encapsulate the code used to parse a ID3v2 extended
		/// header into usable variables.
		/// <para/>
		/// Given that arrays have an inherent Length property, the first four
		/// bytes (storing the size) are ignored.
		/// </summary>
		/// 
		/// <remarks>
		/// This takes a `byte[]` rather than a `Stream` like
		/// <see cref="ReadExtHeaderWithTagAsync(Stream, uint, bool, bool)"/>
		/// because this is intended to be called on pre-processed data of the
		/// proper length, rather than the raw bytestream.
		/// </remarks>
		/// 
		/// <param name="extHeader">
		/// The de-unsynchronized byte array to parse.
		/// </param>
		protected abstract void ParseExtendedHeader(byte[] extHeader);

		/// <summary>
		/// Compares the CRC saved in the tag with that calculated from the
		/// given data to ensure no corruption has occurred.
		/// </summary>
		/// <param name="tag">
		/// The data over which to calculate the CRC.
		/// </param>
		/// <returns>
		/// True if no CRC was saved or if it matches that calculated for the
		/// data, false if they differ.
		/// </returns>
		protected bool CheckCRCIfPresent(byte[] tag) {
			if (TagCRC.HasValue)
				return (TagCRC.Value == Force.Crc32.Crc32Algorithm.Compute(tag));
			else
				return true;
		}
	}
}