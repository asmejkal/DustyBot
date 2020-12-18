namespace DustyBot.Framework.Commands.Parsing
{
    public enum CommandParseResultType
    {
        Success,
        InvalidPrefix,
        InvalidInvoker,
        MissingVerbs,
        InvalidVerb,
        NotEnoughParameters,
        InvalidParameterFormat,
        TooManyParameters
    }
}
