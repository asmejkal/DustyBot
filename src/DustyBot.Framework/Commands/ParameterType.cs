using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DustyBot.Framework.Commands
{
    public enum ParameterType
    {
        Short,
        UShort,
        Int,
        UInt,
        Long,
        ULong,
        Double,
        Float,
        Decimal,
        Bool,
        String,
        Uri,
        Guid,
        Regex,
        ColorCode,

        Id,
        TextChannel,
        GuildUser,
        GuildUserOrName,
        Role,
        GuildUserMessage,
        GuildSelfMessage
    }
}
