/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.Collections.Generic;

namespace CardCatalog.Audio {
	/// <summary>
	/// Common format-agnostic attributes specific to audio encoding, mapping
	/// to different fields depending on the particular metadata format.
	/// </summary>
	/// 
	/// <seealso cref="TagField"/>
	public abstract class AudioTagAttributes : ITagAttributes {
		/// <summary>
		/// The display name of the enclosing file.
		/// </summary>
		public abstract IEnumerable<string> Name { get; }
	}
}
