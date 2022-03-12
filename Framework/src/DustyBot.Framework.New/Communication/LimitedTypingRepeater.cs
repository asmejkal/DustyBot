using System;
using System.Threading;
using Disqord;
using Disqord.Rest;
using Disqord.Rest.Repetition;

namespace DustyBot.Framework.Communication
{
    public class LimitedTypingRepeater : TypingRepeater
    {
        public LimitedTypingRepeater(
            IRestClient client,
            Snowflake channelId,
            TimeSpan timeout,
            IRestRequestOptions? options = null,
            CancellationToken cancellationToken = default) 
            : base(client, channelId, options, CreateToken(timeout, cancellationToken))
        {
        }

        private static CancellationToken CreateToken(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            return cts.Token;
        }
    }
}
