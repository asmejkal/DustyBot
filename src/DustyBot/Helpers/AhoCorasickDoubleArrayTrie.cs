using NReco.Text;
using System.Collections.Generic;
using System.Linq;

namespace DustyBot.Helpers
{
    public class AhoCorasickDoubleArrayTrie : AhoCorasickDoubleArrayTrie<byte>
    {
        public AhoCorasickDoubleArrayTrie(IEnumerable<string> keywords, bool ignoreCase = false)
            : base(keywords.ToDictionary(x => x, x => (byte)0), ignoreCase)
        {
        }
    }
}
