using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DustyBot.Core.Net
{
    public static class WebRequestExtensions
    {
        public static async Task<WebResponse> GetResponseAsync(this WebRequest request, CancellationToken ct)
        {
            using (ct.Register(() => request.Abort(), useSynchronizationContext: false))
            {
                try
                {
                    var response = await request.GetResponseAsync();
                    return response;
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.RequestCanceled && ct.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(ex.Message, ex, ct);
                    }

                    throw;
                }
            }
        }

        public static async Task<HttpWebResponse> GetResponseAsync(this HttpWebRequest request, CancellationToken ct) =>
            (HttpWebResponse)await GetResponseAsync((WebRequest)request, ct);
    }
}
