﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AgEitilt.CardCatalog.Audio.ID3v2 {
	/// <summary>
	/// Shared code for all versions of the ID3v2 standard.
	/// </summary>
	public abstract partial class ID3v2 : AudioTagFormat {
		/// <summary>
		/// ID3v2 string fields use ISO-8859-1 by default but it does not have
		/// a static reference like <see cref="System.Text.Encoding.UTF8"/>,
		/// so provide one rather than needing to call
		/// <see cref="System.Text.Encoding.GetEncoding(string)"/> each time.
		/// </summary>
		protected static System.Text.Encoding ISO88591 = System.Text.Encoding.GetEncoding(28591);

		/// <summary>
		/// The minor version number of the specification used.
		/// </summary>
		protected byte VersionMinor { get; private set; }

		/// <summary>
		/// Whether the header includes a non-standard tag, which may result
		/// in unrecognizable data.
		/// </summary>
		/// 
		/// <remarks>
		/// TODO: Store data about the unknown flags rather than simply
		/// indicating their presence.
		/// </remarks>
		protected bool FlagUnknown { get; set; }

		/// <summary>
		/// Initialize instance properties to default values.
		/// </summary>
		public ID3v2() {
			VersionMinor = 0x00;
			FlagUnknown = false;
		}

		/// <summary>
		/// Retrieve the proper number of bytes from the stream to contain the
		/// header.
		/// </summary>
		/// 
		/// <param name="stream">The stream to read from.</param>
		/// 
		/// <returns>The number of bytes used by a ID3 header.</returns>
		protected static byte[] RetrieveHeader(Stream stream) {
			var bytes = new byte[10];
			stream.Read(bytes, 0, 10);

			return bytes;
		}

		/// <summary>
		/// "Rewind" retrieving the header so that the stream is left in the
		/// same state as it started in.
		/// </summary>
		/// 
		/// <param name="stream">The stream to rewind.</param>
		protected static void UnreadHeader(Stream stream) {
			stream.Position -= 10;
		}

		/// <summary>
		/// Check whether the byte array begins with a valid ID3v2 header.
		/// </summary>
		/// 
		/// <param name="header">The sequence of bytes to check</param>
		/// 
		/// <returns>
		/// `null` if the stream does not begin with a ID3v2 header, and the
		/// major version if it does.
		/// </returns>
		protected static byte? VerifyBaseHeader(IEnumerable<byte> header) {
			var headerBytes = header.ToArray();

			// If it's shorter than the length, the header can never be valid
			if (headerBytes.Length < 10)
				return null;
			// Check against the specification
			else if ((headerBytes[0] == 0x49)    // 'I'
				&& (headerBytes[1] == 0x44)      // 'D'
				&& (headerBytes[2] == 0x33)      // '3'
				&& (headerBytes[3] < 0xFF)
				&& (headerBytes[4] < 0xFF)
				// No restriction on header[5]
				&& (headerBytes[6] < 0x80)
				&& (headerBytes[7] < 0x80)
				&& (headerBytes[8] < 0x80)
				&& (headerBytes[9] < 0x80))
				return headerBytes[3];
			else
				return null;
		}

		/// <summary>
		/// Manipulate the byte array to remove the historic synchronization
		/// pattern, according to the ID3v2 specifications.
		/// </summary>
		/// 
		/// <param name="input">The byte array to unsynchronize.</param>
		/// <param name="changed">
		/// Whether the synchronization pattern was encountered and
		/// subsequently interrupted.
		/// </param>
		/// 
		/// <returns>A new, synchronization-safe byte array.</returns>
		/// 
		/// <seealso cref="DeUnsynchronize(byte[])"/>
		protected static byte[] Unsynchronize(byte[] input, out bool changed) =>
			Unsynchronize(input, out changed, out bool ignore);
		/// <summary>
		/// Manipulate the byte array to remove the historic synchronization
		/// pattern, according to the ID3v2 specifications.
		/// </summary>
		/// 
		/// <param name="input">The byte array to unsynchronize.</param>
		/// <param name="changed">
		/// Whether the synchronization pattern was encountered and
		/// subsequently interrupted.
		/// </param>
		/// <param name="endPadding">
		/// Whether the last byte in <paramref name="input"/> was 0xFF, which
		/// needs an extra byte of padding if the tag is unsynchronized.
		/// <para/>
		/// If (<paramref name="changed"/> == true), this padding `0x00` byte
		/// is automatically added, but if not (and if a separate -- probably
		/// later -- tag requires unsynchronization), the byte needs to be
		/// appended manually.
		/// </param>
		/// 
		/// <returns>A new, synchronization-safe byte array.</returns>
		/// 
		/// <seealso cref="DeUnsynchronize(byte[])"/>
		protected static byte[] Unsynchronize(byte[] input, out bool changed, out bool endPadding) {
			changed = false;
			endPadding = false;

			/* There will never be less than `input.Length` bytes in the
             * output, and the synchronization will never add more than the
             * number of 0xFF bytes in the array
             */
			var ret = new List<byte>(input.Length + input.Count(test => test == 0xFF));

			for (uint i = 0; i < input.Length; ++i) {
				ret.Add(input[i]);

				if (input[i] == 0xFF) {
					if (i == (input.Length - 1)) {
						// Only add the padding if we can verify that the
						// unsynchronization is necessary
						if (changed == true)
							ret.Add(0x00);
						endPadding = true;
					} else if ((input[i + 1] >= 0xE0) || (input[i + 1] == 0x00)) {
						ret.Add(0x00);
						changed = true;
					}
				}
			}

			return ret.ToArray();
		}

		/// <summary>
		/// Reverse the unsynchronization scheme as described in the ID3v2
		/// specifications.
		/// 
		/// </summary>
		/// <param name="input">
		/// The byte array on which to reverse unsynchronization.
		/// </param>
		/// 
		/// <returns>The pre-unsynchronization byte array.</returns>
		/// 
		/// <exception cref="InvalidDataException">
		/// <paramref name="input"/> is expected to be unsynchronized and a
		/// basic sanity check is performed to ensure this, but attempting to
		/// reconstruct a malformed byte array is beyond the intended scope.
		/// </exception>
		/// 
		/// <seealso cref="Unsynchronize(byte[], out bool, out bool)"/>
		protected static byte[] DeUnsynchronize(byte[] input) {
			var ret = new List<byte>(input.Length);

			for (uint i = 0; i < input.Length; ++i) {
				ret.Add(input[i]);

				if (input[i] == 0xFF) {
					++i;

					if (i < input.Length) {
						// 0x00 is added to break up the synchronization
						// pattern and should be skipped
						if (input[i] == 0x00)
							continue;
						// These characters should never occur in a properly
						// unsynchronized tag
						else if (input[i] >= 0xE0)
							throw new InvalidDataException(Strings.ID3v2.Exceptions.NotUnsynchronized);
						else
							ret.Add(input[i]);
					}
				}
			}

			return ret.ToArray();
		}

		/// <summary>
		/// Asynchronously read a given number of bytes from a stream,
		/// optionally reversing ID3v2 unsynchronization.
		/// </summary>
		/// 
		/// <param name="stream">The stream to read from.</param>
		/// <param name="count">The number of bytes to read.</param>
		/// <param name="unsync">
		/// Whether the stream is unsynchronized, in which case the original
		/// data is restored before being returned.
		/// </param>
		/// 
		/// <returns>
		/// The Task tracking the byte retrieval operation (number of bytes
		/// may be less than <paramref name="count"/>).
		/// </returns>
		protected static Task<byte[]> ReadBytesAsync(Stream stream, uint count, bool unsync = false) {
			byte[] bytes = new byte[count];
			var read = stream.ReadAsync(bytes, 0, bytes.Length);

			Task<byte[]> ret;
			if (unsync)
				ret = read.ContinueWith(_ => DeUnsynchronize(bytes),
					TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously
				);
			else
				ret = read.ContinueWith(_ => bytes, TaskContinuationOptions.OnlyOnRanToCompletion);

			return ret;
		}


		/// <summary>
		/// Retrieve a given number of bytes from a stream, properly handling
		/// ID3v2 unsynchronization.
		/// </summary>
		/// 
		/// <param name="stream">The stream to read from.</param>
		/// <param name="count">
		/// The number of bytes to retrieve, which is updated to reflect the
		/// actual number of bytes read.
		/// </param>
		/// 
		/// <returns>
		/// The specified number of de-unsynchronized bytes.
		/// </returns>
		/// 
		/// <exception cref="EndOfStreamException">
		/// The end of the stream is reached before retrieving the desired
		/// number of de-unsynchronized bytes.
		/// </exception>
		/// <exception cref="InvalidDataException">
		/// <paramref name="stream"/> is expected to be unsynchronized and a
		/// basic sanity check is performed to ensure this, but attempting to
		/// reconstruct a malformed byte array is beyond the intended scope.
		/// </exception>
		protected static byte[] GetUnsyncronizedBytes(Stream stream, ref uint count) {
			var ret = new byte[count];

			bool isPrevFF = false;
			for (uint i = 0; i < ret.Length; ++i) {
				var b = stream.ReadByte();
				if (b < 0)
					throw new EndOfStreamException(String.Format(Strings.ID3v2.Exceptions.StreamEnded, count));
				else
					ret[i] = (byte)b;

				if (isPrevFF) {
					if (ret[i] == 0x00) {
						// Overwrite the current index on the next iteration
						// to skip the byte
						--i;
						// Indicate that an additional byte was read
						++count;
					} else if (b >= 0xE0)
						throw new InvalidDataException(Strings.ID3v2.Exceptions.NotUnsynchronized);
				}

				isPrevFF = (ret[i] == 0xFF);
			}

			return ret;
		}

		/// <summary>
		/// Extract and encapsulate the code used to parse a ID3v2 header into
		/// usable variables.
		/// </summary>
		/// 
		/// <param name="header">The sequence of bytes to check.</param>
		/// 
		/// <returns>The flag bits in a more accessible format.</returns>
		protected BitArray ParseBaseHeader(IEnumerable<byte> header) {
			/* Should fail loudly if the tag's not in the correct format, but
             * since this should only be called from the validation function,
             * can simplify avoiding infinite loops by not checking
             */

			var headerBytes = header.ToArray();

			VersionMinor = headerBytes[4];

			// Decompose the flags byte
			var flags = new BitArray(new byte[1] { headerBytes[5] });

			// Calculate the size from 7-bit "bytes" (the high bit is ignored)
			Length = (int)ParseUnsignedInteger(header.Skip(6).ToArray(), 7u);

			return flags;
		}

		/// <summary>
		/// Read a variable number of bytes as a single integer.
		/// </summary>
		/// 
		/// <param name="stream">The source to read from.</param>
		/// <param name="unsynced">
		/// Whether the source has been unsynchronized.
		/// </param>
		/// <param name="bits">The number of data bits per byte.</param>
		/// <param name="count">The number of bytes to read.</param>
		/// 
		/// <returns>The value after combining all bytes.</returns>
		/// 
		/// <exception cref="ArgumentOutOfRangeException">
		/// Numbers must fit within the proper storage data type (typically
		/// <paramref name="count"/> must not be more than four bytes for
		/// ID3v2.3 and five for ID3v2.4).
		/// </exception>
		/// <exception cref="EndOfStreamException">
		/// The end of the stream is reached before retrieving the desired
		/// number of de-unsynchronized bytes.
		/// </exception>
		/// <exception cref="InvalidDataException">
		/// <paramref name="stream"/> is expected to be unsynchronized and a
		/// basic sanity check is performed to ensure this, but attempting to
		/// reconstruct a malformed byte array is beyond the intended scope.
		/// </exception>
		protected static uint ParseUnsignedInteger(Stream stream, bool unsynced, ref uint count, uint bits = 8) {
			if ((bits * count) > (sizeof(uint) * 8))
				throw new ArgumentOutOfRangeException(Strings.ID3v2.Exceptions.ParsedIntTooLarge);

			byte[] bytes;
			if (unsynced)
				bytes = GetUnsyncronizedBytes(stream, ref count);
			else {
				bytes = new byte[count];
				stream.Read(bytes, 0, (int)count);
			}

			return ParseUnsignedInteger(bytes, bits);
		}
		/// <summary>
		/// Read a variable number of bytes as a single integer.
		/// </summary>
		/// 
		/// <param name="bytes">The source to read from.</param>
		/// <param name="bits">The number of data bits per byte.</param>
		/// 
		/// <returns>The value after combining all bytes.</returns>
		/// 
		/// <exception cref="ArgumentOutOfRangeException">
		/// Numbers must fit within the proper storage data type (typically
		/// <paramref name="bytes"/> must not be more than four bytes long for
		/// ID3v2.3 and five for ID3v2.4).
		/// </exception>
		protected static uint ParseUnsignedInteger(byte[] bytes, uint bits = 8) {
			if ((bits * bytes.Length) > (sizeof(uint) * 8))
				throw new ArgumentOutOfRangeException(Strings.ID3v2.Exceptions.ParsedIntTooLarge);

			uint ret = 0;
			for (uint i = 0; i < bytes.Length; ++i)
				ret |= ((uint)bytes[i] << (int)(bits * (bytes.Length - i - 1)));

			return ret;
		}
		/// <summary>
		/// Read a variable number of bytes as a single integer.
		/// </summary>
		/// 
		/// <param name="bytes">The source to read from.</param>
		/// <param name="bits">The number of data bits per byte.</param>
		/// 
		/// <returns>The value after combining all bytes.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Numbers must fit within the proper storage data type (typically
		/// <paramref name="bytes"/> must not be more than four bytes long for
		/// ID3v2.3 and five for ID3v2.4).
		/// </exception>
		protected static uint ParseUnsignedInteger(IEnumerable<byte> bytes, uint bits = 8) {
			if ((bits * bytes.Count()) > (sizeof(uint) * 8))
				throw new ArgumentOutOfRangeException(Strings.ID3v2.Exceptions.ParsedIntTooLarge);

			uint ret = 0;
			var iter = bytes.GetEnumerator();
			for (uint i = 0; i < bytes.Count(); ++i, iter.MoveNext())
				ret |= ((uint)iter.Current << (int)(bits * (bytes.Count() - i - 1)));

			return ret;
		}

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
	}

	/// <summary>
	/// Extension methods for the <see cref="ImageData"/> class.
	/// </summary>
	public static class ImageCategoryExtension {
		/// <summary>
		/// Convert a <see cref="ID3v2.ImageCategory"/> value to a
		/// human-readable string for the current locale.
		/// </summary>
		/// 
		/// <param name="value">
		/// The <see cref="ID3v2.ImageCategory"/> to format.
		/// </param>
		/// 
		/// <returns>The formatted name.</returns>
		public static string PrintableName(this ID3v2.ImageCategory value) {
			var str = value.ToString();

			// If `str` is purely digits, the lookup has failed
			if (str.All(char.IsDigit) == false) {
				var genre = Strings.ID3v2.Images.ResourceManager.GetString(str);
				if (genre != null)
					return genre;
			}
			return String.Format(Strings.ID3v2.Images.Unknown, str);
		}
	}
}