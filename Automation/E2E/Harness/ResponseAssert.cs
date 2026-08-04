using System;
using System.Linq;
using Disqord.Gateway;
using DustyBot.Framework.Communication;
using Xunit;

namespace DustyBot.Automation.E2E.Harness
{
    /// <summary>
    /// Loose assertions against DustyBot's command acknowledgements: only the leading marker is checked
    /// (see CommunicationConstants).
    /// </summary>
    public static class ResponseAssert
    {
        public static void IsSuccess(IGatewayUserMessage message) =>
            Assert.StartsWith(CommunicationConstants.SuccessMarker, message.Content);

        public static void IsFailure(IGatewayUserMessage message) =>
            Assert.StartsWith(CommunicationConstants.FailureMarker, message.Content);

        /// <summary>
        /// Checks the message's content or any of its embeds (title/description/footer/fields) for a substring -
        /// for responses that aren't Success/Failure acks (e.g. DustyModuleBase.Result/Pages/View results, which
        /// carry no marker and may render as plain text or as an embed depending on the command).
        /// </summary>
        public static void Contains(IGatewayUserMessage message, string expectedSubstring) =>
            Assert.True(HasSubstring(message, expectedSubstring),
                $"Expected the message (content or any embed) to contain \"{expectedSubstring}\". Content: \"{message.Content}\"");

        /// <summary>The negation of <see cref="Contains"/> - e.g. confirming a removed item no longer appears in a listing.</summary>
        public static void DoesNotContain(IGatewayUserMessage message, string unexpectedSubstring) =>
            Assert.False(HasSubstring(message, unexpectedSubstring),
                $"Expected the message (content or any embed) to not contain \"{unexpectedSubstring}\". Content: \"{message.Content}\"");

        private static bool HasSubstring(IGatewayUserMessage message, string substring)
        {
            var inContent = message.Content?.Contains(substring, StringComparison.OrdinalIgnoreCase) == true;
            var inEmbeds = message.Embeds.Any(e =>
                (e.Title?.Contains(substring, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Description?.Contains(substring, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Footer?.Text?.Contains(substring, StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.Fields.Any(f =>
                    f.Name.Contains(substring, StringComparison.OrdinalIgnoreCase) ||
                    f.Value.Contains(substring, StringComparison.OrdinalIgnoreCase)));

            return inContent || inEmbeds;
        }
    }
}
