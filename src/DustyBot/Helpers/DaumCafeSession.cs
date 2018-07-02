using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DustyBot.Framework.Utility;
using System.Security;
using System.Net.Http;

namespace DustyBot.Helpers
{
    /// <summary>
    /// Daum API is dead, so we have to go the browser route...
    /// </summary>
    public class DaumCafeSession : IDisposable
    {
        private static DaumCafeSession _anonymous = new DaumCafeSession();
        public static DaumCafeSession Anonymous => _anonymous;

        private HttpClient _client;
        private HttpClientHandler _handler;

        private Tuple<string, SecureString> _credential;

        private DaumCafeSession()
        {
            _handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };
            _client = new HttpClient(_handler);
        }

        private DaumCafeSession(string user, SecureString password)
        {
            _credential = Tuple.Create(user, password);
            _handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };
            _client = new HttpClient(_handler);
        }

        public static async Task<DaumCafeSession> Create(string user, SecureString password)
        {
            var instance = new DaumCafeSession(user, password);
            await instance.Authenticate();

            return instance;
        }

        public async Task<int> GetLastPostId(string cafeId, string boardId)
        {
            return (await GetPostIds(cafeId, boardId).ConfigureAwait(false)).DefaultIfEmpty().Max();
        }

        public async Task<List<int>> GetPostIds(string cafeId, string boardId)
        {
            var content = await _client.GetStringAsync($"http://m.cafe.daum.net/{cafeId}/{boardId}").ConfigureAwait(false);
            var result = new List<int>();

            await Task.Run(() =>
            {
                foreach (Match match in Regex.Matches(content, $"{cafeId}/{boardId}/([0-9]+)[\"\\/]"))
                {
                    if (match.Groups.Count < 2)
                        continue;

                    int id;
                    if (!int.TryParse(match.Groups[1].Value, out id))
                        continue;

                    result.Add(id);
                }
            }).ConfigureAwait(false);

            return result;
        }

        public class PageBody
        {
            private static Regex _subjectRegex = new Regex(@"<h3.*?class=""tit_subject"".*?>(.*?)<\/\s*h3>", RegexOptions.Compiled | RegexOptions.Singleline);
            private static Regex _textRegex = new Regex(@"<div.*?id=""article"".*?>(.*?)</\s*div>", RegexOptions.Compiled | RegexOptions.Singleline);
            private static Regex _imageRegex = new Regex(@"<img.*?src=[\""'](.+?)[\""'].*?>", RegexOptions.Compiled | RegexOptions.Singleline);
            private static Regex _removeHtmlTagsRegex = new Regex(@"(?></?\w+)(?>(?:[^>'""]+|'[^']*'|""[^""]*"")*)>", RegexOptions.Compiled | RegexOptions.Singleline);
            private static Regex _htmlLineBreakRegex = new Regex(@"<\s*br\s*[\/]?\s*>", RegexOptions.Compiled | RegexOptions.Singleline);

            public string Subject;
            public string Text;
            public string ImageUrl;

            public static PageBody Create(string content)
            {
                var result = new PageBody();

                var match = _subjectRegex.Match(content);
                if (match.Success)
                    result.Subject = _removeHtmlTagsRegex.Replace(match.Groups[1].Value, string.Empty).Trim();

                match = _textRegex.Match(content);
                if (match.Success)
                {
                    var article = match.Groups[1].Value;
                    match = _imageRegex.Match(article);
                    if (match.Success)
                        result.ImageUrl = match.Groups[1].Value;

                    article = _htmlLineBreakRegex.Replace(article, "\n");
                    result.Text = _removeHtmlTagsRegex.Replace(article, string.Empty).Trim();
                }

                return result;
            }

            private static Regex _commentsTextRegex = new Regex(@"<span.*?class=""txt_detail"".*?>(.*?)</\s*span>", RegexOptions.Compiled | RegexOptions.Singleline);
            private static Regex _commentsImageRegex = new Regex(@"<img.*?src=[\""'](.+?C120x120\/\?fname=.+?)[\""'].*?>", RegexOptions.Compiled | RegexOptions.Singleline);

            public static PageBody CreateComment(string content)
            {
                var result = new PageBody();

                var match = _commentsTextRegex.Match(content);
                if (match.Success)
                {
                    var article = match.Groups[1].Value;
                    article = _htmlLineBreakRegex.Replace(article, "\n");
                    result.Text = _removeHtmlTagsRegex.Replace(article, string.Empty).Trim();
                }

                match = _commentsImageRegex.Match(content);
                if (match.Success)
                {
                    result.ImageUrl = match.Groups[1].Value.Replace("C120x120", "R640x0");
                }

                return result;
            }
        }

        public class PageMetadata
        {
            public string RelativeUrl { get; set; }
            public string Type { get; set; }
            public string Title { get; set; }
            public string ImageUrl { get; set; }
            public string Description { get; set; }

            public PageBody Body { get; set; }
        }
        
        private static Regex _metaPropertyRegex = new Regex(@"<meta\s+property=""(.+)"".+content=""(.+)"".*>", RegexOptions.Compiled);

        public async Task<PageMetadata> GetPageMetadata(string mobileUrl)
        {
            var content = await _client.GetStringAsync(mobileUrl).ConfigureAwait(false);
            var properties = new List<Tuple<string, string>>();

            await Task.Run(() =>
            {
                var matches = _metaPropertyRegex.Matches(content);
                foreach (Match match in matches)
                    properties.Add(Tuple.Create(match.Groups[1].Value, match.Groups[2].Value));

            }).ConfigureAwait(false);
            
            var url = properties.FirstOrDefault(x => x.Item1 == "og:url")?.Item2;
            if (!string.IsNullOrEmpty(url) && url.Contains("comments"))
            {
                //Comment type board
                return new PageMetadata()
                {
                    RelativeUrl = url,
                    Type = "comment",
                    Body = PageBody.CreateComment(content)
                };
            }
            else
            {
                //Assume regular board
                return new PageMetadata()
                {
                    RelativeUrl = url,
                    Type = properties.FirstOrDefault(x => x.Item1 == "og:type")?.Item2,
                    Title = WebUtility.HtmlDecode(properties.FirstOrDefault(x => x.Item1 == "og:title")?.Item2 ?? ""),
                    ImageUrl = properties.FirstOrDefault(x => x.Item1 == "og:image")?.Item2,
                    Description = WebUtility.HtmlDecode(properties.FirstOrDefault(x => x.Item1 == "og:description")?.Item2 ?? ""),
                    Body = PageBody.Create(content)
                };
            }
        }

        public async Task Authenticate()
        {
            var request = WebRequest.CreateHttp($"https://logins.daum.net/accounts/login.do");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.CookieContainer = new CookieContainer();

            using (var stream = await request.GetRequestStreamAsync())
            using (var writer = new StreamWriter(stream, System.Text.Encoding.ASCII, 1, false))
            {
                await writer.WriteAsync($"id={_credential.Item1}&pw=");
                await _credential.Item2.ForEach(async x => { await writer.WriteAsync((char)x); });
            }

            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            {
                //Expire old cookies
                foreach (Cookie cookie in _handler.CookieContainer.GetCookies(new Uri("http://daum.net")))
                    cookie.Expired = true;

                if (response.ResponseUri.ToString().Contains("releasecountryrestrict"))
                    throw new CountryBlockException(); //Failed because of logging in from a different country

                if (response.Cookies.Count <= 0)
                    throw new LoginFailedException();

                //Add new cookies
                foreach (Cookie cookie in response.Cookies)
                    _handler.CookieContainer.Add(new Uri($"http://{cookie.Domain}{cookie.Path}"), cookie);
            }
        }

        #region IDisposable 

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _client?.Dispose();
                    _client = null;
                }

                _disposed = true;
            }
        }

        //~()
        //{
        //    Dispose(false);
        //}

        #endregion
    }

    public class CountryBlockException : Exception
    {
        public CountryBlockException() { }
    }

    public class LoginFailedException : ArgumentException
    {
        public LoginFailedException() { }
    }
}
