using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace DustyBot.Framework.Utility
{
    internal class KeyValueSpecificationParser
    {
        public enum ErrorType
        {
            ValidationFailed,
            RequiredPartMissing,
            DuplicatedUniquePart,
            MissingDependency,
        }

        public class ParseResult
        {
            public class Match
            {
                public string? Name { get; }
                public string Value { get; set; }

                public Match(string? name, string value)
                {
                    Name = name;
                    Value = value ?? throw new ArgumentNullException(nameof(value));
                }
            }

            [MemberNotNullWhen(false, nameof(ErrorPart))]
            public bool Succeeded { get; }
            public ErrorType Error { get; }
            public KeyValueSpecificationPart? ErrorPart { get; }

            public IEnumerable<PartMatch> Matches { get; }

            public ParseResult(IEnumerable<PartMatch> matches)
            {
                Succeeded = true;
                Matches = matches;
            }

            public ParseResult(ErrorType error, KeyValueSpecificationPart errorPart)
            {
                Succeeded = false;
                Error = error;
                ErrorPart = errorPart;
                Matches = Enumerable.Empty<PartMatch>();
            }
        }

        public class PartMatch
        {
            public KeyValueSpecificationPart Part { get; }
            public ParseResult.Match Match { get; }

            public PartMatch(KeyValueSpecificationPart part, ParseResult.Match match)
            {
                Part = part ?? throw new ArgumentNullException(nameof(part));
                Match = match ?? throw new ArgumentNullException(nameof(match));
            }

            public void Deconstruct(out KeyValueSpecificationPart part, out ParseResult.Match match)
            {
                part = Part;
                match = Match;
            }
        }

        private readonly IReadOnlyCollection<KeyValueSpecificationPart> _parts;

        public KeyValueSpecificationParser(IEnumerable<KeyValueSpecificationPart> parts)
        {
            _parts = parts?.ToList() ?? throw new ArgumentNullException(nameof(parts));
        }

        public ParseResult Parse(string specification)
        {
            var results = new List<PartMatch>();
            var current = (PartMatch?)null;

            bool AddCurrentResult(PartMatch current)
            {
                current.Match.Value = current.Match.Value.Trim();
                if (current.Part.Validator != null && !current.Part.Validator(current.Match.Name, current.Match.Value))
                    return false;

                results.Add(current);
                return true;
            }

            using var reader = new StringReader(specification);
            for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                var match = _parts
                    .Select(x => new { Part = x, Match = x.TokenRegex.Match(line) })
                    .FirstOrDefault(x => x.Match.Success);

                if (match != null)
                {
                    if (current != null && !AddCurrentResult(current))
                        return new ParseResult(ErrorType.ValidationFailed, current.Part);

                    var newMatch = match.Part.IsNameAccepted
                        ? new ParseResult.Match(match.Match.Groups[1].Value, match.Match.Groups[2].Value)
                        : new ParseResult.Match(null, match.Match.Groups[1].Value);

                    current = new PartMatch(match.Part, newMatch);
                }
                else if (current != default)
                {
                    current.Match.Value += '\n' + line;
                }
            }

            if (current != null && !AddCurrentResult(current))
                return new ParseResult(ErrorType.ValidationFailed, current.Part);

            var missingPart = _parts.FirstOrDefault(x => x.IsRequired && !results.Any(y => y.Part == x));
            if (missingPart != null)
                return new ParseResult(ErrorType.RequiredPartMissing, missingPart);

            var duplicatedPart = _parts.FirstOrDefault(x => x.IsUnique && results.Count(y => y.Part == x) > 1);
            if (duplicatedPart != null)
                return new ParseResult(ErrorType.DuplicatedUniquePart, duplicatedPart);

            var missingDependency = _parts
                .Where(x => !string.IsNullOrEmpty(x.DependsOn) && results.Any(y => y.Part == x))
                .FirstOrDefault(x => !results.Any(y => y.Part.Token == x.DependsOn));

            if (missingDependency != null)
                return new ParseResult(ErrorType.MissingDependency, missingDependency);

            return new ParseResult(results);
        }
    }
}
