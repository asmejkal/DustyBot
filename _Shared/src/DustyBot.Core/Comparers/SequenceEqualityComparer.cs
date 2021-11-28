using System;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Core.Comparers
{
    public class SequenceEqualityComparer<T> : IEqualityComparer<IEnumerable<T>>
    {
        public bool Equals(IEnumerable<T> x, IEnumerable<T> y)
        {
            return ReferenceEquals(x, y) || (x != null && y != null && x.SequenceEqual(y));
        }

        public int GetHashCode(IEnumerable<T> obj)
        {
            unchecked
            {
                return obj.Where(e => e != null).Select(e => e.GetHashCode()).Aggregate(17, (a, b) => 23 * a + b);
            }
        }
    }
}
