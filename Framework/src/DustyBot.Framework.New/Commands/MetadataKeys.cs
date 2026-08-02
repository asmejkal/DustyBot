namespace DustyBot.Framework.Commands
{
    public static class MetadataKeys
    {
        public const string Remarks = "DustyBot.Remarks";
        public const string CorrelationId = "DustyBot.CorrelationId";

        /// <summary>
        /// Marks a module builder as one of the synthetic per-verb-segment modules created by
        /// <see cref="DustyBotSharderBase"/> to represent <see cref="Attributes.VerbCommandAttribute"/> paths.
        /// Can't use <c>TypeInfo == null</c> for this: command execution needs these modules' TypeInfo set
        /// to their source module's type (see the comment where this constant is used), so TypeInfo being
        /// non-null no longer implies "this is a real, reflection-backed module".
        /// </summary>
        public const string IsVerbModule = "DustyBot.IsVerbModule";
    }
}
