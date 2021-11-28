using System;
using System.Collections.Generic;
using System.Linq;
using Qmmands;

namespace DustyBot.Framework.Attributes
{
    public class VerbCommandAttribute : CommandAttribute
    {
        public IReadOnlyCollection<string> Verbs { get; }

        public VerbCommandAttribute(params string[] tokens)
            : base(tokens.Last())
        {
            if (tokens == null)
                throw new ArgumentNullException(nameof(tokens));

            if (tokens.Length < 2)
                throw new ArgumentException("Verb commands must contain at least one verb.", nameof(tokens));

            if (tokens.Any(x => string.IsNullOrEmpty(x)))
                throw new ArgumentException("Verb commands can't have empty verbs or aliases.", nameof(tokens));

            Verbs = tokens.SkipLast(1).ToList();
        }
    }
}
