using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata.Audio {
    /// <summary>
    /// An implementation of the ID3v2.3 standard as described at
    /// <see cref="http://id3.org/d3v2.3.0"/>
    /// </summary>
    internal class ID3v23 : ID3v23Plus {
        /// <summary>
        /// The short name used to represent ID3v2.3 metadata.
        /// </summary>
        /// <seealso cref="Metadata.Register{TFormat}(string, Func{Stream, bool})"/>
        public const string format = "ID3v2.3";

        /// <summary>
        /// Describe the behaviour of the extended header.
        /// </summary>
        public override ExtendedHeaderProps ExtendedHeader => new ExtendedHeaderProps {
            sizeIncludesItself = false,
            bitsInSize = 8
        };

        /// <summary>
        /// Register the format with the superclass.
        /// </summary>
        /// <seealso cref="Metadata.Register{TFormat}(string, Func{Stream, bool})"/>
        static ID3v23() {
            Metadata.Register<ID3v23>(format, VerifyHeader);
        }

        /// <summary>
        /// Check whether the stream begins with a valid ID3v2.3 header.
        /// </summary>
        /// <param name="stream">The Stream to check.</param>
        /// <returns>
        /// Whether the stream begins with a valid ID3v2.3 header.
        /// </returns>
        /// <see cref="Metadata.Validate(string, Stream)"/>
        public static bool VerifyHeader(Stream stream) {
            return (VerifyBaseHeader(stream)?.Equals(0x03) ?? false);
        }
        /// <summary>
        /// Check whether the byte array begins with a valid ID3v2.3 header.
        /// </summary>
        /// <param name="header">The byte array to check</param>
        /// <returns>
        /// `null` if the stream does not begin with a ID3v2.3 header, and the
        /// major version if it does.
        /// </returns>
        public static bool VerifyHeader(byte[] header) {
            return (VerifyBaseHeader(header)?.Equals(0x03) ?? false);
        }

        /// <summary>
        /// The size of the empty padding at the end of the tag.
        /// </summary>
        private uint PaddingSize { get; set; }

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
        public ID3v23(Stream stream) {
            PaddingSize = 0;

            byte[] tag = ParseHeaderAsync(stream).Result;
            if (CheckCRCIfPresent(tag.Take((int)(tag.Length - PaddingSize)).ToArray()) == false)
                throw new InvalidDataException("ID3 tag does not match saved checksum");
        }

        /// <summary>
        /// Extract and encapsulate the code used to parse a ID3v2 header into
        /// usable variables, and use that to retrieve the rest of the tag.
        /// </summary>
        /// <param name="stream">The stream to parse.</param>
        /// <returns>
        /// The remainder of the ID3v2.3 tag, already processed to reverse any
        /// unsynchronization.
        /// </returns>
        async Task<byte[]> ParseHeaderAsync(Stream stream) {
            var header = ParseBaseHeader(stream, VerifyHeader);

            bool useUnsync = header.Item1[0];
            // flags[1] is handled below
            IsExperimental = header.Item1[2];
            /*TODO: May be better to skip reading the tag rather than setting
             * FlagUnknown, as these flags tend to be critical to the proper
             * parsing of the tag.
             */
            FlagUnknown = (header.Item1.Cast<bool>().Skip(3).Contains(true));

            return await ReadExtHeaderWithTagAsync(stream, header.Item2, useUnsync, header.Item1[1]).ConfigureAwait(false);
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
        /// <see cref="ParseHeaderAsync(Stream)"/> because this is intended to
        /// be /// called on pre-processed data of the proper length, rather
        /// than the raw bytestream.
        /// </remarks>
        /// <param name="extHeader">
        /// The de-unsynchronized byte array to parse.
        /// </param>
        protected override void ParseExtendedHeader(byte[] extHeader) {
            if (extHeader.Length < 6)
                throw new InvalidDataException("Extended header too short to be valid for ID3v2.3");

            var flags = new BitArray(extHeader.Take(2).ToArray());
            PaddingSize = ParseInteger(extHeader.Skip(2).Take(4));

            if (flags[0]) {
                if (extHeader.Length < 10)
                    throw new InvalidDataException("Extended header too short to contain a valid ID3v2.3 CRC");

                TagCRC = ParseInteger(extHeader.Skip(6).Take(4));
            }
            FlagUnknown = (flags.Cast<bool>().Skip(1).Contains(true));
        }
    }
}