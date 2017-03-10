namespace Metadata {
	/// <summary>
	/// Miscellaneous helper functions and classes.
	/// </summary>
	public static class Helpers {
		/// <summary>
		/// Retrieve the requested value from the dictionary, creating a new
		/// entry if necessary.
		/// </summary>
		/// 
		/// <typeparam name="TKey">
		/// The type used for the dictionary's keys.
		/// </typeparam>
		/// <typeparam name="TValue">
		/// The type used for the dictionary's values.
		/// </typeparam>
		/// 
		/// <param name="dictionary">The dictionary on which to operate.</param>
		/// <param name="key">The dictionary key to access.</param>
		/// 
		/// <returns>The object located at <paramref name="key"/> in
		/// <paramref name="dictionary"/>.</returns>
		public static TValue GetOrCreate<TKey, TValue>(this System.Collections.Generic.Dictionary<TKey, TValue> dictionary, TKey key)
				where TValue : new() {
			if (dictionary.ContainsKey(key) == false)
				dictionary.Add(key, new TValue());

			return dictionary[key];
		}
	}
}
