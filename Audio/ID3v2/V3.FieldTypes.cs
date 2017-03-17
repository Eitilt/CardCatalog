﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections;
using System.Linq;

namespace CardCatalog.Audio.ID3v2 {
	partial class V3 {
		/// <summary>
		/// Fields specific to the ID3v2.3 standard.
		/// </summary>
		public class FieldTypes {
			Tuple<byte[], BitArray> ReadHeader(byte[] header, out uint length) {
				if (header.Length < 10)
					throw new ArgumentException("Need ten bytes to read a ID3v2.3 header", "header");

				length = ParseUnsignedInteger(header.ToList().GetRange(4, 4));
				return Tuple.Create(
					header.Take(4).ToArray(),
					new BitArray(new byte[2] { header[8], header[9] })
				);
			}
		}
	}
}
