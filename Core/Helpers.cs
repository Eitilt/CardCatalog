using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardCatalog {
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
		/// <param name="dictionary">
		/// The <see cref="Dictionary{TKey, TValue}"/> on which to operate.
		/// </param>
		/// <param name="key">The dictionary key to access.</param>
		/// 
		/// <returns>
		/// The object located at <paramref name="key"/> in
		/// <paramref name="dictionary"/>.
		/// </returns>
		public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
				where TValue : new() {
			if (dictionary.ContainsKey(key) == false)
				dictionary.Add(key, new TValue());

			return dictionary[key];
		}

		/// <summary>
		/// Test sequences for equality based on their values, not their
		/// object references.
		/// </summary>
		/// 
		/// <remarks>
		/// Implementation from <see href="http://stackoverflow.com/a/7244729"/>.
		/// </remarks>
		/// 
		/// <typeparam name="ElementType">
		/// The underlying type of the sequence.
		/// </typeparam>
		public class SequenceEqualityComparer<ElementType> : IEqualityComparer<ElementType[]> {
			/// <summary>
			/// Provide a more robust meant of testing the equality of
			/// elements.
			/// </summary>
			static readonly EqualityComparer<ElementType> elementComparer = EqualityComparer<ElementType>.Default;

			/// <summary>
			/// Check two sequences for value equality.
			/// </summary>
			/// 
			/// <returns>Whether the sequences are equal.</returns>
			public bool Equals(ElementType[] x, ElementType[] y) {
				if (x == y)
					return true;
				else if ((x == null) || (y == null))
					return false;

				if (x.Length != y.Length)
					return false;

				return x.SequenceEqual(y, elementComparer);
			}

			/// <summary>
			/// Calculate a hash code based on the values of the sequence.
			/// </summary>
			/// 
			/// <param name="obj">The sequence to hash.</param>
			/// 
			/// <returns>The calculated hash.</returns>
			public int GetHashCode(ElementType[] obj) {
				if (obj == null)
					return 0;

				int hash = 17;
				foreach (ElementType t in obj) {
					hash *= 31;
					hash += elementComparer.GetHashCode(t);
				}

				return hash;
			}
		}
		
		/// <summary>
		/// Read from the stream until it ends or the requested number of
		/// bytes has been reached.
		/// </summary>
		/// 
		/// <param name="stream">
		/// The <see cref="Stream"/> on which to operate.
		/// </param>
		/// <param name="buffer">
		/// The destination in which to save the read bytes.
		/// </param>
		/// <param name="offset">
		/// The index in <paramref name="buffer"/> of the first read byte.
		/// </param>
		/// <param name="count">The number of bytes to read.</param>
		/// 
		/// <returns>
		/// The number of bytes that were successfully read. This may be less
		/// than <paramref name="count"/> if the stream ended before that
		/// number of bytes was reached.
		/// </returns>
		public static int ReadAll(this Stream stream, byte[] buffer, int offset, int count) {
			int total = 0;

			while (count > 0) {
				int read = stream.Read(buffer, offset, count);
				if (read == 0) {
					break;
				} else {
					offset += read;
					total += read;
					count -= read;
				}
			}

			return total;
		}
	}
}
