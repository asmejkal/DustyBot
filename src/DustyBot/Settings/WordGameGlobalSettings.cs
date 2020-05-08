using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DustyBot.Framework.LiteDB;
using LiteDB;
using DustyBot.Definitions;

namespace DustyBot.Settings
{
    //public class WordGameGlobalSettings : BaseSettings
    //{
    //    public Dictionary<WordGameDictionaryId, WordGameDictionary> Dictionaries { get; set; } =
    //        new Dictionary<WordGameDictionaryId, WordGameDictionary>();
    //}

    //public class WordGameDictionaryId : IEquatable<WordGameDictionaryId>
    //{
    //    public string Language { get; set; }
    //    public string Name { get; set; }

    //    public override bool Equals(object obj) => Equals(obj as WordGameDictionaryId);

    //    public bool Equals(WordGameDictionaryId other)
    //    {
    //        return other != null &&
    //               string.Compare(Language, other.Language, true, GlobalDefinitions.Culture) == 0 &&
    //               string.Compare(Name, other.Name, true, GlobalDefinitions.Culture) == 0;
    //    }

    //    public override int GetHashCode() => HashCode.Combine(Language, Name);

    //    public static bool operator ==(WordGameDictionaryId left, WordGameDictionaryId right) => 
    //        EqualityComparer<WordGameDictionaryId>.Default.Equals(left, right);

    //    public static bool operator !=(WordGameDictionaryId left, WordGameDictionaryId right) => !(left == right);
    //}

    //public class WordGameDictionary
    //{
    //    public WordGameDictionaryId Id { get; set; }
    //    public ulong Author { get; set; }

    //    public Dictionary<string, List<string>> Entries { get; set; } = new Dictionary<string, List<string>>();
    //}
}
