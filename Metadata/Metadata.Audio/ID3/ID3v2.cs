using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata.Audio {
    /// <summary>
    /// Shared code for all versions of the ID3v2 standard.
    /// </summary>
    public abstract class ID3v2 : AudioTagFormat {
        /// <summary>
        /// The minor version number of the specification used.
        /// </summary>
        protected byte VersionMinor { get; private set; }

        /// <summary>
        /// Whether the header includes a non-standard tag, which may result
        /// in unrecognizable data.
        /// </summary>
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
        /// <param name="stream">The stream to read from.</param>
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
        /// <param name="stream">The stream to rewind.</param>
        protected static void UnreadHeader(Stream stream) {
            stream.Position -= 10;
        }

        /// <summary>
        /// Check whether the stream begins with a valid ID3v2 header.
        /// </summary>
        /// <param name="stream">The Stream to check.</param>
        /// <returns>
        /// `null` if the stream does not begin with a ID3v2 header, and the
        /// major version if it does.
        /// </returns>
        /// <see cref="MetadataFormat.Validate(string, Stream)"/>
        protected static byte? VerifyBaseHeader(Stream stream) {
            byte? ret = VerifyBaseHeader(RetrieveHeader(stream));
            UnreadHeader(stream);

            return ret;
        }
        /// <summary>
        /// Check whether the byte array begins with a valid ID3v2 header.
        /// </summary>
        /// <param name="header">The byte array to check</param>
        /// <returns>
        /// `null` if the stream does not begin with a ID3v2 header, and the
        /// major version if it does.
        /// </returns>
        protected static byte? VerifyBaseHeader(byte[] header) {
            // If it's shorter than the length, the header can never be valid
            if (header.Length < 10)
                return null;
            // Check against the specification
            else if ((header[0] == 0x49)    // 'I'
                || (header[1] == 0x44)      // 'D'
                || (header[2] == 0x33)      // '3'
                || (header[3] < 0xFF)
                || (header[4] < 0xFF)
                // No restriction on header[5]
                || (header[6] < 0x80)
                || (header[7] < 0x80)
                || (header[8] < 0x80)
                || (header[9] < 0x80))
                return header[3];
            else
                return null;
        }

        /// <summary>
        /// Manipulate the byte array to remove the historic synchronization
        /// pattern, according to the ID3v2 specifications.
        /// </summary>
        /// <param name="input">The byte array to unsynchronize.</param>
        /// <param name="changed">
        /// Whether the synchronization pattern was encountered and
        /// subsequently interrupted.
        /// </param>
        /// <returns>A new, synchronization-safe byte array.</returns>
        /// <seealso cref="DeUnsynchronize(byte[])"/>
        protected static byte[] Unsynchronize(byte[] input, out bool changed) {
            return Unsynchronize(input, out changed, out bool ignore);
        }
        /// <summary>
        /// Manipulate the byte array to remove the historic synchronization
        /// pattern, according to the ID3v2 specifications.
        /// </summary>
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
        /// <returns>A new, synchronization-safe byte array.</returns>
        /// <seealso cref="DeUnsynchronize(byte[])"/>
        protected static byte[] Unsynchronize(byte[] input, out bool changed, out bool endPadding) {
            changed = false;
            endPadding = false;

            /* There will never be less than `input.Length` bytes in the
             * output, and the synchronization will never add more than the
             * number of 0xFF bytes in the array
             */
            var ret = new List<byte>(input.Length + input.Count((byte test) => test == 0xFF));

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
        /// </summary>
        /// <param name="input">
        /// The byte array on which to reverse unsynchronization.
        /// </param>
        /// <returns>The pre-unsynchronization byte array.</returns>
        /// <exception cref="InvalidDataException">
        /// <paramref name="input"/> is expected to be unsynchronized and a
        /// basic sanity check is performed to ensure this, but attempting to
        /// reconstruct a malformed byte array is beyond the intended scope.
        /// </exception>
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
                            throw new InvalidDataException("Attempted to reverse ID3v2 unsynchronization on a stream with an invalid byte following 0xFF");
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
        /// <param name="stream">The stream to read from.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <param name="unsync">
        /// Whether the stream is unsynchronized, in which case it's reversed.
        /// </param>
        /// <returns>
        /// The Task tracking the byte retrieval operation (number of bytes
        /// may be less than <paramref name="count"/>).
        /// </returns>
        protected static Task<byte[]> ReadBytesAsync(Stream stream, uint count, bool unsync = false) {
            byte[] bytes = new byte[count];
            var read = stream.ReadAsync(bytes, 0, bytes.Length);

            Task<byte[]> ret;
            if (unsync)
                ret = read.ContinueWith((antecedent) => DeUnsynchronize(bytes),
                    TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously
                );
            else
                ret = read.ContinueWith((antecedent) => bytes, TaskContinuationOptions.OnlyOnRanToCompletion);

            return ret;
        }


        /// <summary>
        /// Retrieve a given number of bytes from a stream, properly handling
        /// ID3v2 unsynchronization.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="count">
        /// The number of bytes to retrieve, which is updated to reflect the
        /// actual number of bytes read.
        /// </param>
        /// <returns>
        /// The specified number of de-unsynchronized bytes.
        /// </returns>
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
                    throw new EndOfStreamException("Reached end of ID3v2 stream while trying to read " + count + " unsynchronized bytes");
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
                        throw new InvalidDataException("Attempted to reverse ID3v2 unsynchronization on a stream with an invalid byte following 0xFF");
                }

                isPrevFF = (ret[i] == 0xFF);
            }

            return ret;
        }

        /// <summary>
        /// Extract and encapsulate the code used to parse a ID3v2 header into
        /// usable variables.
        /// </summary>
        /// <param name="stream">The stream to parse.</param>
        /// <param name="validation">A function </param>
        /// <returns>
        /// A tuple of, in order:
        /// <list type="bullet">
        /// <item>The flag bits in a more accessible format.</item>
        /// <item>The size of the remainder of the tag.</item>
        /// </list>
        /// </returns>
        protected Tuple<BitArray, uint> ParseBaseHeader(Stream stream, Func<byte[], bool> validation) {
            var header = RetrieveHeader(stream);

            // Fail loudly if the tag's not in the correct format.
            if (validation(header) == false) {
                UnreadHeader(stream);
                throw new FormatException("Stream does not begin with a valid ID3 header");
            }

            VersionMinor = header[4];

            // Decompose the flags byte
            var flags = new BitArray(new byte[1] { header[5] });

            // Calculate the size from 7-bit "bytes" (high bit is ignored)
            uint size = ParseInteger(header.Skip(6).ToArray(), 7u);

            return new Tuple<BitArray, uint>(flags, size);
        }

        /// <summary>
        /// Read a variable number of bytes as a single integer.
        /// </summary>
        /// <param name="stream">The source to read from.</param>
        /// <param name="unsynced">
        /// Whether the source has been unsynchronized.
        /// </param>
        /// <param name="bits">The number of data bits per byte.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The value after combining all bytes.</returns>
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
        protected static uint ParseInteger(Stream stream, bool unsynced, ref uint count, uint bits = 8) {
            if ((bits * count) > (sizeof(uint) * 8))
                throw new ArgumentOutOfRangeException("Attempting to read a larger integer from ID3 stream than supported by the storage type");

            byte[] bytes;
            if (unsynced)
                bytes = GetUnsyncronizedBytes(stream, ref count);
            else {
                bytes = new byte[count];
                stream.Read(bytes, 0, (int)count);
            }

            return ParseInteger(bytes, bits);
        }
        /// <summary>
        /// Read a variable number of bytes as a single integer.
        /// </summary>
        /// <param name="bytes">The source to read from.</param>
        /// <param name="bits">The number of data bits per byte.</param>
        /// <returns>The value after combining all bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Numbers must fit within the proper storage data type (typically
        /// <paramref name="bytes"/> must not be more than four bytes long for
        /// ID3v2.3 and five for ID3v2.4).
        /// </exception>
        protected static uint ParseInteger(byte[] bytes, uint bits = 8) {
            if ((bits * bytes.Length) > (sizeof(uint) * 8))
                throw new ArgumentOutOfRangeException("Attempting to read a larger integer from ID3 stream than supported by the storage type");

            uint ret = 0;
            for (uint i = 0; i < bytes.Length; ++i)
                ret |= ((uint)bytes[i] << (int)(bits * (bytes.Length - i - 1)));

            return ret;
        }
        /// <summary>
        /// Read a variable number of bytes as a single integer.
        /// </summary>
        /// <param name="bytes">The source to read from.</param>
        /// <param name="bits">The number of data bits per byte.</param>
        /// <returns>The value after combining all bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Numbers must fit within the proper storage data type (typically
        /// <paramref name="bytes"/> must not be more than four bytes long for
        /// ID3v2.3 and five for ID3v2.4).
        /// </exception>
        protected static uint ParseInteger(IEnumerable<byte> bytes, uint bits = 8) {
            if ((bits * bytes.Count()) > (sizeof(uint) * 8))
                throw new ArgumentOutOfRangeException("Attempting to read a larger integer from ID3 stream than supported by the storage type");

            uint ret = 0;
            var iter = bytes.GetEnumerator();
            for (uint i = 0; i < bytes.Count(); ++i, iter.MoveNext())
                ret |= ((uint)iter.Current << (int)(bits * (bytes.Count() - i - 1)));

            return ret;
        }
    }


    /// <summary>
    /// Shared code for ID3v2.3 and later.
    /// </summary>
    public abstract class ID3v23Plus : ID3v2 {
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
        /// Indicates that the tag is in an experimental stage.
        /// </summary>
        /// <remarks>Just as ill-defined in the ID3v2 specification.</remarks>
        protected bool IsExperimental { get; set; }
        /// <summary>
        /// The CRC calculated for the data in the tag, or `null` if is was
        /// not (yet) read.
        /// </summary>
        protected uint? TagCRC { get; set; }

        /// <summary>
        /// Initialize instance properties to default values.
        /// </summary>
        public ID3v23Plus() {
            IsExperimental = false;
            TagCRC = null;
        }

        /// <summary>
        /// Parse an ID3v2 extended header starting at the current position in
        /// the stream, while retrieving the remainder of the tag in the
        /// background.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="tagSize">The total size of the ID3v2 tag.</param>
        /// <param name="useUnsync">
        /// Whether the entire tag has been unsynchronized.
        /// </param>
        /// <param name="extendedHeaderPresent">
        /// Whether the tag contains an extended header.
        /// </param>
        /// <returns>
        /// The remainder of the ID3v2 tag, already processed to reverse any
        /// unsynchronization.
        /// </returns>
        protected async Task<byte[]> ReadExtHeaderWithTagAsync(Stream stream, uint tagSize, bool useUnsync, bool extendedHeaderPresent) {
            if (extendedHeaderPresent == false)
                return await ReadBytesAsync(stream, tagSize, useUnsync).ConfigureAwait(false);

            uint sizeCount = 4;
            uint extSize = ParseInteger(stream, useUnsync, ref sizeCount, ExtendedHeader.bitsInSize);

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
        /// <remarks>
        /// This takes a `byte[]` rather than a `Stream` like
        /// <see cref="ReadExtHeaderWithTagAsync(Stream, uint, bool, bool)"/>
        /// because this is intended to be called on pre-processed data of the
        /// proper length, rather than the raw bytestream.
        /// </remarks>
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