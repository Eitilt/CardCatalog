using System;
using System.Collections.Generic;
using System.Linq;

namespace Metadata {
    /// <summary>
    /// An alias for <see cref="Dictionary{TKey, TValue}"/> to ensure the
    /// proper equality of keys to <see cref="TagField"/> objects.
    /// </summary>
    public class FieldDictionary : Dictionary<byte[], IEnumerable<TagField>>, IReadOnlyFieldDictionary {
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
        class SequenceEqualityComparer<ElementType> : IEqualityComparer<IEnumerable<ElementType>> {
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
            public bool Equals(IEnumerable<ElementType> x, IEnumerable<ElementType> y) {
                if (x == y)
                    return true;
                else if ((x == null) || (y == null))
                    return false;

                if (x.Count() != y.Count())
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
            public int GetHashCode(IEnumerable<ElementType> obj) {
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
        /// An instance of <see cref="SequenceEqualityComparer{ElementType}"/>
        /// specialized to byte arrays.
        /// </summary>
        static SequenceEqualityComparer<byte> keyComparer = new SequenceEqualityComparer<byte>();

        /// <summary>
        /// Initializes a new instance of the <see cref="FieldDictionary"/>
        /// class that is empty and has the default initial capacity.
        /// </summary>
        /// 
        /// <remarks>
        /// Every key in a <see cref="FieldDictionary"/> must have a unique
        /// sequence of values.
        /// <para/>
        /// If you can estimate the size of the collection, using
        /// <see cref="FieldDictionary(int)"/> eliminates the need to perform
        /// a number of resizing operations while adding elements.
        /// <para/>
        /// This constructor is an O(1) operation.
        /// </remarks>
        public FieldDictionary() : base(keyComparer) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="FieldDictionary"/>
        /// class that is empty and has the specified initial capacity.
        /// </summary>
        /// 
        /// <remarks>
        /// Every key in a <see cref="FieldDictionary"/> must have a unique
        /// sequence of values.
        /// <para/>
        /// The capacity of a <see cref="FieldDictionary"/> is the number of
        /// elements that can be added to the <see cref="FieldDictionary"/>
        /// before resizing is necessary. As elements are added to a
        /// <see cref="FieldDictionary"/>, the capacity is automatically
        /// increased as required by reallocating the internal array.
        /// Therefore, if the largest size of the collection can be estimated,
        /// specifying an initial capacity of that size or greater eliminates
        /// the need to perform a number of resizing operations while adding
        /// elements to the <see cref="FieldDictionary"/>.
        /// <para/>
        /// This constructor is an O(1) operation.
        /// </remarks>
        /// 
        /// <param name="capacity">
        /// The initial number of elements that the
        /// <see cref="FieldDictionary"/> can contain.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="capacity"/> is less than 0.
        /// </exception>
        public FieldDictionary(int capacity) : base(capacity, keyComparer) { }
        /// <summary>
        /// Initializes a new instance of a <see cref="FieldDictionary"/>
        /// that contains elements copied from <paramref name="dictionary"/>.
        /// </summary>
        /// 
        /// <remarks>
        /// Every key in a <see cref="FieldDictionary"/> must have a unique
        /// sequence of values; every key in <paramref name="dictionary"/>
        /// must therefore also have a unique sequence of values.
        /// <para/>
        /// IThe initial capacity of the new <see cref="FieldDictionary"/> is
        /// large enough to contain all the elements in
        /// <paramref name="dictionary"/>.
        /// <para/>
        /// This constructor is an O(n) operation, where n is the number of
        /// elements in <paramref name="dictionary"/>.
        /// </remarks>
        /// 
        /// <param name="dictionary">
        /// The <see cref="FieldDictionary"/> whose elements are copied to the
        /// new <see cref="FieldDictionary"/>.
        /// </param>
        public FieldDictionary(FieldDictionary dictionary) : base(dictionary, keyComparer) { }
    }

    /// <summary>
    /// An alias for <see cref="Dictionary{TKey, TValue}"/> to ensure the
    /// proper format for keys to <see cref="TagField"/> objects.
    /// </summary>
    public interface IReadOnlyFieldDictionary : IReadOnlyDictionary<byte[], IEnumerable<TagField>> { }
}
