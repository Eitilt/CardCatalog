using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metadata {
	public class ID32 : Metadata {
        public const string format = "ID3v2";

        static ID32() {
            Register<ID32>(format, VerifyHeader);
        }

        static byte[] ReadHeader(Stream stream) {
            var bytes = new byte[10];
            stream.Read(bytes, 0, 10);

            return bytes;
        }

        public static bool VerifyHeader(Stream stream) {
            bool ret = VerifyHeader(ReadHeader(stream));
            stream.Position -= 10;

            return ret;
        }
        static bool VerifyHeader(byte[] header) {
            if (header.Length < 10)
                return false;
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


        byte versionMajor, versionMinor;
        bool useUnsync, isExperimental, hasFooter, hasCRC, flagUnknown;

        public ID32(Stream stream) {
            var size = ParseHeader(stream);

            if ((versionMajor < 2) || (versionMajor > 4))
                throw new FormatException("Parsing ID3v2." + versionMajor + " tags currently not supported");

            // As below, this is a magic number indicating that the tag is
            // compressed and should be skipped
            if (size >= 0x80000000) {
                stream.Position += (size ^ 0x80000000);
                //TODO: Provide an alternative if .Position/.Seek(...) isn't supported
            }
        }

        uint ParseHeader(Stream stream) {
            var header = ReadHeader(stream);

            if (VerifyHeader(header) == false)
                throw new FormatException("Stream does not begin with a valid ID3 header");

            versionMajor = header[3];
            versionMinor = header[4];

            var flags = new BitArray(new byte[1] { header[5] });
            flagUnknown = ((header[5] & 0x0F) != 0);

            useUnsync = flags[0];
            isExperimental = flags[2];
            hasFooter = flags[3];

            uint size = (header[9]
                + ((uint)header[8] << 7)
                + ((uint)header[7] << 14)
                + ((uint)header[6] << 21));

            if ((versionMajor == 2) && flags[1])
                /* Unfortunately a bit of a magic number, but need some way of
                 * indicating that the tag's compressed while also returning
                 * the size, as the ID3v2.2 specification recommends ignoring
                 * it in that case but the parser should scan to the end for
                 * consistent behaviour with all other tags
                 */
                return (size | 0x80000000);
            else if (flags[1])
                return (size - ParseExtendedHeader(stream));
            else
                return size;
        }

        uint ParseExtendedHeader(Stream stream) {
            return 0;
        }
	}
}