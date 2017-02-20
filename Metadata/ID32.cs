using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metadata {
    public class ID32 : Metadata.ITag {
        #region Static members
        /// <summary>
        /// The short name used to represent ID3v2 metadata.
        /// </summary>
        /// <seealso cref="Metadata.Register{TFormat}(string, Func{Stream, bool})"/>
        public const string format = "ID3v2";

        /// <summary>
        /// Register the format with the superclass.
        /// </summary>
        /// <seealso cref="Metadata.Register{TFormat}(string, Func{Stream, bool})"/>
        static ID32() {
            Metadata.Register<ID32>(format, VerifyHeader);
        }

        /// <summary>
        /// Retrieve the proper number of bytes from the stream to contain the
        /// header.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>The number of bytes used by a ID3 header.</returns>
        static byte[] ReadHeader(Stream stream) {
            var bytes = new byte[10];
            stream.Read(bytes, 0, 10);

            return bytes;
        }

        /// <summary>
        /// "Rewind" retrieving the header so that the stream is left in the
        /// same state as it started in.
        /// </summary>
        /// <param name="stream">The stream to rewind.</param>
        static void UnreadHeader(Stream stream) {
            stream.Position -= 10;
        }

        /// <summary>
        /// Check whether the stream begins with a valid ID3v2 header.
        /// </summary>
        /// <param name="stream">The Stream to check.</param>
        /// <returns>Whether the stream begins with a valid ID3 header.</returns>
        /// <see cref="Metadata.Validate(string, Stream)"/>
        public static bool VerifyHeader(Stream stream) {
            bool ret = VerifyHeader(ReadHeader(stream));
            UnreadHeader(stream);

            return ret;
        }
        /// <summary>
        /// Check whether the byte array begins with a valid ID3v2 header.
        /// </summary>
        /// <param name="header">The byte array to check</param>
        /// <returns>Whether the byte array begins with a valid ID3v2 header.</returns>
        static bool VerifyHeader(byte[] header) {
            // If it's shorter than the length, the header can never be valid
            if (header.Length < 10)
                return false;
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
                return true;
            else
                return false;
        }

        /// <summary>
        /// Manipulate the byte array to remove the historic synchronization
        /// pattern, according to the ID3v2 specifications.
        /// </summary>
        /// <param name="input">The byte array to unsynchronize.</param>
        /// <param name="changed">
        /// Whether the synchronization pattern was encountered and
        /// subsequentally interrupted.
        /// </param>
        /// <returns>A new, synchronization-safe byte array.</returns>
        /// <seealso cref="DeUnsynchronize(byte[])"/>
        static byte[] Unsynchronize(byte[] input, out bool changed) {
            return Unsynchronize(input, out changed, out bool ignore);
        }
        /// <summary>
        /// Manipulate the byte array to remove the historic synchronization
        /// pattern, according to the ID3v2 specifications.
        /// </summary>
        /// <param name="input">The byte array to unsynchronize.</param>
        /// <param name="changed">
        /// Whether the synchronization pattern was encountered and
        /// subsequentally interrupted.
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
        static byte[] Unsynchronize(byte[] input, out bool changed, out bool endPadding) {
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
        /// basic sanity check is perfomed to ensure this, but attempting to
        /// reconstruct a malformed byte array is beyond the intended scope.
        /// </exception>
        /// <seealso cref="Unsynchronize(byte[], out bool, out bool)"/>
        static byte[] DeUnsynchronize(byte[] input) {
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

        static Task<byte[]> ReadBytesAsync(Stream stream, uint count, bool unsync) {
            byte[] bytes = new byte[count];
            var read = stream.ReadAsync(bytes, 0, bytes.Length);

            Task<byte[]> ret;
            if (unsync)
                ret = read.ContinueWith((prevTask) => DeUnsynchronize(bytes),
                    TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously
                );
            else
                ret = read.ContinueWith((prevTask) => bytes, TaskContinuationOptions.OnlyOnRanToCompletion);

            return ret;
        }


        /// <summary>
        /// Retrieve a given number of bytes from a stream, properly handling
        /// ID3v2 unsynchronization.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="count">
        /// The number of bytes to retrieve; changes to refleck the actual
        /// number of bytes that were read from the stream.
        /// </param>
        /// <returns>The specified number of de-unsynchronized bytes.</returns>
        /// <exception cref="EndOfStreamException">
        /// The end of the stream is reached before retrieving the desired
        /// number of de-unsynchronized bytes.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// <paramref name="stream"/> is expected to be unsynchronized and a
        /// basic sanity check is perfomed to ensure this, but attempting to
        /// reconstruct a malformed byte array is beyond the intended scope.
        /// </exception>
        static byte[] ReadUnsyncronizedBytes(Stream stream, ref uint count) {
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
        #endregion

        #region Instance members
        /// <summary>
        /// Major version number of the ID3 specification, indicating breaking
        /// changes.
        /// </summary>
        byte versionMajor;
        /// <summary>
        /// Minor version number of the ID3 specification, indicating
        /// backwards-compatible changes.
        /// </summary>
        byte versionMinor;

        /// <summary>
        /// Indicates that the tag is in an experimental stage.
        /// </summary>
        /// <remarks>Just as ill-defined in the ID3v2 specification.</remarks>
        bool isExperimental;
        /// <summary>
        /// Whether the tag is closed with a footer.
        /// </summary>
        bool hasFooter;
        /// <summary>
        /// Whether a CRC has been calculated for the data in the tag.
        /// </summary>
        bool hasCRC;
        /// <summary>
        /// Whether the header includes a non-standard tag, which may result in unrecognizable data.
        /// </summary>
        bool flagUnknown;

        /// <summary>
        /// Parse a stream according the the proper version of the ID3v2
        /// specification, from the current location.
        /// </summary>
        /// <remarks>
        /// As according to the recommendation in the ID3v2.2 specification,
        /// if the tag is compressed, it is swallowed but largely ignored.
        /// </remarks>
        /// <param name="stream">The stream to parse.</param>
        /// <exception cref="FormatException">
        /// This class can only parse metadata in ID3v2.2 to ID3v2.4 formats,
        /// and fails if the stream position is not placed at the beginning of
        /// that tag.
        /// <param/>
        /// If this is thrown, the stream cursor is automatically returned ro
        /// the position it was at before the constructor was called.
        /// </exception>
        /// <seealso cref="Metadata.Construct(string, Stream)"/>
        public ID32(Stream stream) {
            var size = ParseHeader(stream);

            // Major versions are not backwards-compatible and require custom
            // handling
            if ((versionMajor < 2) || (versionMajor > 4)) {
                UnreadHeader(stream);
                throw new FormatException("Parsing ID3v2." + versionMajor + " tags currently not supported");
            }
        }

        /// <summary>
        /// Extract and encapsulate the code used to parse a ID3v2 header into
        /// usable variables.
        /// </summary>
        /// <param name="stream">The stream to parse.</param>
        /// <returns>
        /// The remainder of the tag, properly de-unsynchronized.
        /// </returns>
        byte[] ParseHeader(Stream stream) {
            var header = ReadHeader(stream);

            // Fail loudly if the tag's not in the correct format.
            if (VerifyHeader(header) == false) {
                UnreadHeader(stream);
                throw new FormatException("Stream does not begin with a valid ID3 header");
            }

            versionMajor = header[3];
            versionMinor = header[4];

            // Decompose the flags byte
            var flags = new BitArray(new byte[1] { header[5] });
            flagUnknown = ((header[5] & 0x0F) != 0);

            bool useUnsync = flags[0];
            // flags[1] is handled below
            isExperimental = flags[2];
            hasFooter = flags[3];

            // Calculate the size from 7-bit "bytes" (high bit is ignored)
            uint size = (header[9]
                | ((uint)header[8] << 7)
                | ((uint)header[7] << 14)
                | ((uint)header[6] << 21));

            // ID3v2.2 uses this flag to indicate compression, but recommends
            // ignoring the tag if it's set
            if ((versionMajor == 2) && flags[1])
                return new byte[0];
            // ID3v2.3+ uses this flag to indicate the presence of an extended
            // header, which we can parse while loading the rest of the tag
            else if (flags[1]) {
                uint sizeCount = 4;
                uint extSize = ParseInteger(stream, useUnsync, ref sizeCount);
                byte[] extHeader = ReadUnsyncronizedBytes(stream, ref extSize);

                // Start reading (and processing if necessary) from the stream
                // in the background to save a small bit of time while
                // parsing the extended header
                var deunsync = ReadBytesAsync(stream, (size - sizeCount - extSize), useUnsync);

                ParseExtendedHeader(extHeader);

                return deunsync.Result;
            } else
                return ReadBytesAsync(stream, size, useUnsync).Result;
        }

        /// <summary>
        /// Extract and encapsulate the code used to parse a ID3v2 extended
        /// header into usable variables.
        /// </summary>
        /// <remarks>
        /// This uses a `byte[]` rather than a `Stream` like
        /// <see cref="ParseHeader(Stream)"/> because this is intended to be
        /// called on pre-processed data of the proper length, rather than the
        /// raw bytestream.
        /// </remarks>
        /// <param name="extHeader">
        /// The de-unsynchronized byte array to parse.
        /// </param>
        private void ParseExtendedHeader(byte[] extHeader) {
        }

        /// <summary>
        /// Read a variable number of bytes as a single integer.
        /// </summary>
        /// <param name="stream">The source to read from.</param>
        /// <param name="unsynced">
        /// Whether the source has been unsynchronized.
        /// </param>
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
        /// basic sanity check is perfomed to ensure this, but attempting to
        /// reconstruct a malformed byte array is beyond the intended scope.
        /// </exception>
        uint ParseInteger(Stream stream, bool unsynced, ref uint count) {
            // By making this a variable, we can use the same code for all
            // versions of the standard
            uint offset = (versionMajor == 3 ? 8u : 7u);
            if ((offset * count) > (sizeof(uint) * 8))
                throw new ArgumentOutOfRangeException("Attempting to read a larger integer from ID3 stream than supported by the storage type");

            byte[] bytes;
            if (unsynced)
                bytes = ReadUnsyncronizedBytes(stream, ref count);
            else {
                bytes = new byte[count];
                stream.Read(bytes, 0, (int)count);
            }

            uint ret = 0;
            for (uint i = 0; i < count; ++i)
                ret |= ((uint)bytes[i] << (int)(offset * (count - i - 1)));

            return ret;
        }
        #endregion
    }
}