using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata {
    /// <summary>
    /// An implementation of the ID3v2.2 standard as described at
    /// <see cref="http://id3.org/id3v2-00"/>
    /// </summary>
    internal class ID3v22 : ID3v2 {
        /// <summary>
        /// The short name used to represent ID3v2.2 metadata.
        /// </summary>
        /// <seealso cref="Metadata.Register{TFormat}(string, Func{Stream, bool})"/>
        public const string format = "ID3v2.2";

        /// <summary>
        /// Register the format with the superclass.
        /// </summary>
        /// <seealso cref="Metadata.Register{TFormat}(string, Func{Stream, bool})"/>
        static ID3v22() {
            Metadata.Register<ID3v22>(format, VerifyHeader);
        }

        /// <summary>
        /// Check whether the stream begins with a valid ID3v2.2 header.
        /// </summary>
        /// <param name="stream">The Stream to check.</param>
        /// <returns>
        /// Whether the stream begins with a valid ID3v2.2 header.
        /// </returns>
        /// <see cref="Metadata.Validate(string, Stream)"/>
        public static bool VerifyHeader(Stream stream) {
            return (VerifyBaseHeader(stream)?.Equals(0x02) ?? false);
        }
        /// <summary>
        /// Check whether the byte array begins with a valid ID3v2.2 header.
        /// </summary>
        /// <param name="header">The byte array to check</param>
        /// <returns>
        /// `null` if the stream does not begin with a ID3v2.2 header, and the
        /// major version if it does.
        /// </returns>
        public static bool VerifyHeader(byte[] header) {
            return (VerifyBaseHeader(header)?.Equals(0x02) ?? false);
        }

        /// <summary>
        /// Parse a stream according the proper version of the ID3v2
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
        /// If this is thrown, the stream cursor is automatically returned to
        /// the position it was at before the constructor was called.
        /// </exception>
        /// <seealso cref="Metadata.Construct(string, Stream)"/>
        public ID3v22(Stream stream) {
            byte[] tag = ParseHeaderAsync(stream).Result;
        }

        /// <summary>
        /// Extract and encapsulate the code used to parse a ID3v2 header into
        /// usable variables, and use that to retrieve the rest of the tag.
        /// </summary>
        /// <param name="stream">The stream to parse.</param>
        /// <returns>
        /// The remainder of the tag, properly de-unsynchronized.
        /// </returns>
        async Task<byte[]> ParseHeaderAsync(Stream stream) {
            var header = ParseBaseHeader(stream, VerifyHeader);

            bool useUnsync = header.Item1[0];
            // flags[1] is handled below
            FlagUnknown = (header.Item1.Cast<bool>().Skip(2).Contains(true));

            // ID3v2.2 uses this flag to indicate compression, but recommends
            // ignoring the tag if it's set
            if (header.Item1[1]) {
                stream.Position += header.Item2;
                return new byte[0];
            } else
                return await ReadBytesAsync(stream, header.Item2, useUnsync).ConfigureAwait(false);
        }
    }
}