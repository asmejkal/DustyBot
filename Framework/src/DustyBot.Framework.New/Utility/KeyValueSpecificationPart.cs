using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DustyBot.Framework.Utility
{
    internal class KeyValueSpecificationPart
    {
        public string Token { get; }
        public bool IsUnique { get; }
        public bool IsRequired { get; }
        public string DependsOn { get; }
        public bool IsNameAccepted { get; }
        public Func<string, string, bool> Validator { get; }
        public Regex TokenRegex { get; }

        public KeyValueSpecificationPart(
            string token, 
            bool isUnique, 
            bool isRequired, 
            string dependsOn = null, 
            bool isNameAccepted = false, 
            Func<string, string, bool> validator = null)
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
            IsUnique = isUnique;
            IsRequired = isRequired;
            DependsOn = dependsOn;
            IsNameAccepted = isNameAccepted;
            Validator = validator;

            var pattern = IsNameAccepted ? $"^{token}\\s*\\((.+?)\\)\\s*:\\s*(.+?)\\s*$" : $"^{token}\\s*:\\s*(.+?)\\s*$";
            TokenRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public override bool Equals(object obj) => obj is KeyValueSpecificationPart part && Token == part.Token;

        public override int GetHashCode() => HashCode.Combine(Token);
    }
}
