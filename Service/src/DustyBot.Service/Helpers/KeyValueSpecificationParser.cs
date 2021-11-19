using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DustyBot.Service.Helpers
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
                public string Name { get; set;  }
                public string Value { get; set; }
            }

            public bool Succeeded { get; }
            public ErrorType Error { get; }
            public KeyValueSpecificationPart ErrorPart { get; }

            public IEnumerable<(KeyValueSpecificationPart Part, Match Match)> Matches { get; }

            public ParseResult(IEnumerable<(KeyValueSpecificationPart, Match)> matches)
            {
                Succeeded = true;
                Matches = matches;
            }

            public ParseResult(ErrorType error, KeyValueSpecificationPart errorPart)
            {
                Succeeded = false;
                Error = error;
                ErrorPart = errorPart;
            }
        }

        private readonly IReadOnlyCollection<KeyValueSpecificationPart> _parts;

        public KeyValueSpecificationParser(IEnumerable<KeyValueSpecificationPart> parts)
        {
            _parts = parts?.ToList() ?? throw new ArgumentNullException(nameof(parts));
        }

        public ParseResult Parse(string specification)
        {
            var results = new List<(KeyValueSpecificationPart Part, ParseResult.Match Match)>();
            var current = default((KeyValueSpecificationPart Part, ParseResult.Match Match));

            bool AddCurrentResult()
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
                    if (current != default && !AddCurrentResult())
                        return new ParseResult(ErrorType.ValidationFailed, current.Part);

                    current = (match.Part, new ParseResult.Match());
                    if (match.Part.IsNameAccepted)
                    {
                        current.Match.Name = match.Match.Groups[1].Value;
                        current.Match.Value = match.Match.Groups[2].Value;
                    }
                    else
                    {
                        current.Match.Value = match.Match.Groups[1].Value;
                    }
                }
                else if (current != default)
                {
                    current.Match.Value += '\n' + line;
                }
            }

            if (current != default && !AddCurrentResult())
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
