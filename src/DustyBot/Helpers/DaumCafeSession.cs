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
using HtmlAgilityPack;
using DustyBot.Helpers;
using System.IO.Compression;

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
            : this()
        {
            _credential = Tuple.Create(user, password);
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
            public string Subject;
            public string Text;
            public string ImageUrl;

            public static PageBody Create(string content)
            {
                var result = new PageBody();

                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                result.Subject = doc.DocumentNode.Descendants("h3").FirstOrDefault(x => x.GetAttributeValue("class", "") == "tit_subject")?.InnerText.Trim();

                var text = doc.DocumentNode.Descendants("div").FirstOrDefault(x => x.GetAttributeValue("id", "") == "article");
                if (text != null)
                {
                    result.ImageUrl = text.Descendants("img").FirstOrDefault(x => x.Attributes.Contains("src"))?.GetAttributeValue("src", "").Trim();
                    result.Text = text.ToPlainText().Trim();
                }

                return result;
            }

            public static PageBody CreateComment(string content)
            {
                var result = new PageBody();

                var doc = new HtmlDocument();
                doc.LoadHtml(content);
                
                result.Text = doc.DocumentNode.Descendants("span").FirstOrDefault(x => x.GetAttributeValue("class", "") == "txt_detail")?.ToPlainText().Trim();
                result.ImageUrl = doc.DocumentNode.Descendants("img").FirstOrDefault(x => x.Attributes.Contains("src"))?.GetAttributeValue("src", "").Trim().Replace("C120x120", "R640x0");

                //Discord stopped embedding the scaled down links (eg. https://img1.daumcdn.net/thumb/R640x0/?fname=http://cfile277.uf.daum.net/image/99D447415BA4896424BC9D)
                var i = result.ImageUrl?.LastIndexOf("fname=") ?? -1;
                if (i >= 0)
                    result.ImageUrl = result.ImageUrl.Substring(i + "fname=".Length);

                //Protocol sometimes missing
                if (result.ImageUrl?.StartsWith("//") ?? false)
                    result.ImageUrl = "https:" + result.ImageUrl;

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

        public async Task<PageMetadata> GetPageMetadata(Uri mobileUrl)
        {
            string content;
            var response = await _client.GetAsync(mobileUrl).ConfigureAwait(false);
            if (response.StatusCode == (HttpStatusCode)308)
            {
                //Deal with the wonky 308 status code (permanent redirect) - HttpClient should redirect, but it doesn't (probably because 308 is not even in .NET docs)
                var location = response.Headers.Location;
                var absoluteLocation = location.IsAbsoluteUri ? location : new Uri(new Uri(mobileUrl.GetComponents(UriComponents.Scheme | UriComponents.StrongAuthority, UriFormat.Unescaped)), location);
                content = await _client.GetStringAsync(absoluteLocation).ConfigureAwait(false);
            }
            else
                content = await response.Content.ReadAsStringAsync();

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
            //Daum disabled the simple credential login flow for *some* reason 
            //(3 weeks after this feature has been implemented - makes me wonder...).
            //So now we'll have to imitate the standard no-JS browser login flow.
            //Only the FUID input is newly required, but we'll imitate all the headers 
            //to make this harder to distinguish from normal Chrome users, just in case... (sans the cookies)
            string fuid;
            {
                var request = WebRequest.CreateHttp("https://logins.daum.net/accounts/loginform.do");
                request.Method = "GET";
                request.Headers["Connection"] = "keep-alive";
                request.Headers["Cache-Control"] = "max-age=0";
                request.Headers["Upgrade-Insecure-Requests"] = "1";
                request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36";
                request.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
                request.Headers["Accept-Encoding"] = "gzip, deflate, br";
                request.Headers["Accept-Language"] = "en,cs-CZ;q=0.9,cs;q=0.8";

                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var stream = response.GetResponseStream())
                using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzipStream))
                {
                    var content = await reader.ReadToEndAsync().ConfigureAwait(false);

                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    fuid = doc.DocumentNode.Descendants("input").FirstOrDefault(x => x.GetAttributeValue("name", "") == "fuid" )?.GetAttributeValue("value", "");
                }
            }

            if (string.IsNullOrWhiteSpace(fuid))
                throw new NodeNotFoundException("Cannot find FUID.");

            {
                var request = WebRequest.CreateHttp($"https://logins.daum.net/accounts/login.do");
                request.Method = "POST";
                request.Headers["Connection"] = "keep-alive";
                request.Headers["Cache-Control"] = "max-age=0";
                request.Headers["Origin"] = "https://logins.daum.net";
                request.Headers["Upgrade-Insecure-Requests"] = "1";
                request.ContentType = "application/x-www-form-urlencoded";
                request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36"; //required
                request.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
                request.Headers["Referer"] = "https://logins.daum.net/accounts/loginform.do"; //required
                request.Headers["Accept-Encoding"] = "gzip, deflate, br";
                request.Headers["Accept-Language"] = "en,cs-CZ;q=0.9,cs;q=0.8";
                
                request.CookieContainer = new CookieContainer();

                using (var stream = await request.GetRequestStreamAsync())
                using (var writer = new StreamWriter(stream, System.Text.Encoding.ASCII, 1, false))
                {

                    await writer.WriteAsync($"url=https%3A%2F%2Fwww.daum.net%2F&relative=&weblogin=1&service=&fuid={fuid}&slevel=1&finaldest=&reloginSeq=0&id={_credential.Item1}&pw=");
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
        }

        public async Task<bool> ArePostsAccesible(string cafeId, string boardId)
        {
            List<int> ids;
            try
            {
                ids = await GetPostIds(cafeId, boardId).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new InaccessibleBoardException(string.Empty, ex);
            }

            //Try to retrieve a few post IDs
            int tries = 0;
            foreach (var postId in ids.OrderByDescending(x => x))
            {
                try
                {
                    var _ = await _client.GetStringAsync($"http://m.cafe.daum.net/{cafeId}/{boardId}/{postId}").ConfigureAwait(false);
                    return true;
                }
                catch (HttpRequestException)
                {
                }

                if (++tries >= 10)
                    break;
            }
            
            return false;
        }
        
        private static Regex _boardLinkRegex = new Regex(@".*cafe.daum.net\/(.+)\/(\w+).*", RegexOptions.Compiled);
        private static Regex _bbsBoardLinkRegex = new Regex(@".*cafe.daum.net\/.+\/bbs_list.+fldid=(\w+).*", RegexOptions.Compiled);
        private static Regex _groupCodeRegex = new Regex(@"GRPCODE\s*:\s*""(.+)""", RegexOptions.Compiled);

        public async Task<Tuple<string, string>> GetCafeAndBoardId(string boardUrl)
        {        
            //Check if we're dealing with a BBS board link...
            var match = _bbsBoardLinkRegex.Match(boardUrl);
            if (match.Success)
            {
                try
                {
                    //Gotta make an HTTP request to get the Cafe ID...
                    var content = await _client.GetStringAsync(boardUrl).ConfigureAwait(false);
                    var grpCodeMatch = _groupCodeRegex.Match(content);
                    if (!grpCodeMatch.Success)
                        throw new Exception("Unexpected response.");

                    return Tuple.Create(grpCodeMatch.Groups[1].Value, match.Groups[1].Value);
                }
                catch (HttpRequestException ex)
                {
                    throw new InaccessibleBoardException(string.Empty, ex);
                }
            }
            else
            {
                //Nice and behaving link
                match = _boardLinkRegex.Match(boardUrl);

                if (!match.Success)
                    throw new InvalidBoardLinkException();

                return Tuple.Create(match.Groups[1].Value, match.Groups[2].Value);
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
        public CountryBlockException(string message) { }
        public CountryBlockException(string message, Exception innerException) { }
    }

    public class LoginFailedException : ArgumentException
    {
        public LoginFailedException() { }
        public LoginFailedException(string message) { }
        public LoginFailedException(string message, Exception innerException) { }
    }

    public class InaccessibleBoardException : Exception
    {
        public InaccessibleBoardException() { }
        public InaccessibleBoardException(string message) { }
        public InaccessibleBoardException(string message, Exception innerException) { }
    }

    public class InvalidBoardLinkException : Exception
    {
        public InvalidBoardLinkException() { }
        public InvalidBoardLinkException(string message) { }
        public InvalidBoardLinkException(string message, Exception innerException) { }
    }
}
