using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Security;
using System.Net.Http;
using HtmlAgilityPack;
using System.IO.Compression;
using DustyBot.Core.Security;
using System.Threading;
using DustyBot.Core.Net;
using DustyBot.Helpers.DaumCafe.Exceptions;

namespace DustyBot.Helpers.DaumCafe
{
    /// <summary>
    /// Daum API is dead, so we have to go the browser route...
    /// </summary>
    internal sealed class DaumCafeSession : IDisposable
    {
        public static readonly DaumCafeSession Anonymous = new DaumCafeSession();
        
        private static Regex _metaPropertyRegex = new Regex(@"<meta\s+property=""(.+)"".+content=""(.+)"".*>", RegexOptions.Compiled);
        private static Regex _boardLinkRegex = new Regex(@".*cafe.daum.net\/(.+)\/(\w+).*", RegexOptions.Compiled);
        private static Regex _bbsBoardLinkRegex = new Regex(@".*cafe.daum.net\/.+\/bbs_list.+fldid=(\w+).*", RegexOptions.Compiled);
        private static Regex _groupCodeRegex = new Regex(@"GRPCODE\s*:\s*""(.+)""", RegexOptions.Compiled);

        private readonly HttpClient _client;
        private readonly HttpClientHandler _handler;
        private readonly Tuple<string, SecureString> _credential;

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

        public static async Task<DaumCafeSession> Create(string user, SecureString password, CancellationToken ct)
        {
            var instance = new DaumCafeSession(user, password);
            await instance.Authenticate(ct);

            return instance;
        }

        public async Task<int> GetLastPostId(string cafeId, string boardId, CancellationToken ct)
        {
            return (await GetPostIds(cafeId, boardId, ct)).DefaultIfEmpty().Max();
        }

        public async Task<List<int>> GetPostIds(string cafeId, string boardId, CancellationToken ct)
        {
            var response = await _client.GetAsync($"https://m.cafe.daum.net/{cafeId}/{boardId}", ct);
            var result = new List<int>();

            if (!response.IsSuccessStatusCode)
                return result;

            var content = await response.Content.ReadAsStringAsync();
            foreach (Match match in Regex.Matches(content, $"{cafeId}/{boardId}/([0-9]+)[\"\\/]"))
            {
                if (match.Groups.Count < 2)
                    continue;

                int id;
                if (!int.TryParse(match.Groups[1].Value, out id))
                    continue;

                result.Add(id);
            }

            return result;
        }

        public async Task<DaumCafePage> GetPage(Uri mobileUrl, CancellationToken ct)
        {
            string content;
            var response = await _client.GetAsync(mobileUrl, ct);
            if (response.StatusCode == (HttpStatusCode)308)
            {
                //Deal with the wonky 308 status code (permanent redirect) - HttpClient should redirect, but it doesn't (probably because 308 is not even in .NET docs)
                var location = response.Headers.Location;
                var absoluteLocation = location.IsAbsoluteUri ? location : new Uri(new Uri(mobileUrl.GetComponents(UriComponents.Scheme | UriComponents.StrongAuthority, UriFormat.Unescaped)), location);
                content = await _client.GetStringAsync(absoluteLocation);
            }
            else
                content = await response.Content.ReadAsStringAsync();

            var properties = new List<Tuple<string, string>>();

            var matches = _metaPropertyRegex.Matches(content);
            foreach (Match match in matches)
                properties.Add(Tuple.Create(match.Groups[1].Value, match.Groups[2].Value));
            
            var url = properties.FirstOrDefault(x => x.Item1 == "og:url")?.Item2;
            if (!string.IsNullOrEmpty(url) && url.Contains("comments"))
            {
                //Comment type board
                return new DaumCafePage()
                {
                    RelativeUrl = url,
                    Type = "comment",
                    Body = DaumCafePageBody.CreateFromComment(content)
                };
            }
            else
            {
                //Assume regular board
                return new DaumCafePage()
                {
                    RelativeUrl = url,
                    Type = properties.FirstOrDefault(x => x.Item1 == "og:type")?.Item2,
                    Title = WebUtility.HtmlDecode(properties.FirstOrDefault(x => x.Item1 == "og:title")?.Item2 ?? ""),
                    ImageUrl = properties.FirstOrDefault(x => x.Item1 == "og:image")?.Item2,
                    Description = WebUtility.HtmlDecode(properties.FirstOrDefault(x => x.Item1 == "og:description")?.Item2 ?? ""),
                    Body = DaumCafePageBody.Create(content)
                };
            }
        }

        public async Task Authenticate(CancellationToken ct)
        {
            string fuid;
            {
                var request = WebRequest.CreateHttp("https://logins.daum.net/accounts/signinform.do");
                request.Method = "GET";
                request.Headers["Connection"] = "keep-alive";
                request.Headers["Cache-Control"] = "max-age=0";
                request.Headers["Upgrade-Insecure-Requests"] = "1";
                request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36";
                request.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
                request.Headers["Accept-Encoding"] = "gzip, deflate, br";
                request.Headers["Accept-Language"] = "en,cs-CZ;q=0.9,cs;q=0.8";

                using (var response = await request.GetResponseAsync(ct))
                using (var stream = response.GetResponseStream())
                using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzipStream))
                {
                    var content = await reader.ReadToEndAsync();

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
                request.Headers["Referer"] = "https://logins.daum.net/accounts/signinform.do"; //required
                request.Headers["Accept-Encoding"] = "gzip, deflate, br";
                request.Headers["Accept-Language"] = "en,cs-CZ;q=0.9,cs;q=0.8";
                
                request.CookieContainer = new CookieContainer();

                using (var stream = await request.GetRequestStreamAsync())
                using (var writer = new StreamWriter(stream, System.Text.Encoding.ASCII, 1, false))
                {

                    await writer.WriteAsync($"url=https%3A%2F%2Fwww.daum.net%2F&relative=&weblogin=1&service=&fuid={fuid}&slevel=1&finaldest=&reloginSeq=0&id={_credential.Item1}&pw=");
                    await _credential.Item2.ForEach(async x => { await writer.WriteAsync((char)x); });
                }

                using (var response = await request.GetResponseAsync(ct))
                {
                    //Expire old cookies
                    foreach (Cookie cookie in _handler.CookieContainer.GetCookies(new Uri("https://daum.net")))
                        cookie.Expired = true;

                    if (response.ResponseUri.ToString().Contains("releasecountryrestrict"))
                        throw new CountryBlockException(); //Failed because of logging in from a different country

                    if (response.Cookies.Count <= 0)
                        throw new LoginFailedException();

                    //Add new cookies
                    foreach (Cookie cookie in response.Cookies)
                        _handler.CookieContainer.Add(new Uri($"https://{cookie.Domain}{cookie.Path}"), cookie);
                }
            }
        }

        public async Task<bool> ArePostsAccesible(string cafeId, string boardId, CancellationToken ct)
        {
            List<int> ids;
            try
            {
                ids = await GetPostIds(cafeId, boardId, ct);
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
                    var _ = await _client.GetStringAsync($"https://m.cafe.daum.net/{cafeId}/{boardId}/{postId}");
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
        
        public async Task<Tuple<string, string>> GetCafeAndBoardId(string boardUrl)
        {        
            //Check if we're dealing with a BBS board link...
            var match = _bbsBoardLinkRegex.Match(boardUrl);
            if (match.Success)
            {
                try
                {
                    //Gotta make an HTTP request to get the Cafe ID...
                    var content = await _client.GetStringAsync(boardUrl);
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

        public void Dispose()
        {
            _client.Dispose();
            _handler.Dispose();
        }
    }
}
