/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.IO;

namespace AgEitilt.CardCatalog.Audio {
	/// <summary>
	/// Common properties to retrieve info from multiple audio formats.
	/// </summary>
	public abstract class AudioTagFormat : MetadataTag {
		/// <summary>
		/// The proper standardized field redirects for the enclosing
		/// metadata format.
		/// </summary>
		/// 
		/// <seealso cref="MetadataTag.Fields"/>
		/// <seealso cref="AudioAttributes"/>
		public sealed override ITagAttributes Attributes => (ITagAttributes)AudioAttributes;

		/// <summary>
		/// The standard field redirects extended with attributes specific to
		/// audio metadata.
		/// </summary>
		/// 
		/// <seealso cref="MetadataTag.Fields"/>
		/// <seealso cref="Attributes"/>
		public abstract AudioTagAttributes AudioAttributes { get; }
	}
}
