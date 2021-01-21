using System.Linq;
using HtmlAgilityPack;

namespace DustyBot.Helpers.DaumCafe
{
    public class DaumCafePageBody
    {
        public string Subject { get; private set; }
        public string Text { get; private set; }
        public string ImageUrl { get; private set; }

        public static DaumCafePageBody Create(string content)
        {
            var result = new DaumCafePageBody();

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

        public static DaumCafePageBody CreateFromComment(string content)
        {
            var result = new DaumCafePageBody();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            result.Text = doc.DocumentNode.Descendants("span").FirstOrDefault(x => x.HasClass("txt_detail"))?.ToPlainText().Trim();
            result.ImageUrl = doc.DocumentNode.Descendants("img").FirstOrDefault(x => x.HasClass("thumb_info"))?.GetAttributeValue("src", null)?.Trim().Replace("C120x120", "R640x0");

            // Discord stopped embedding the scaled down links (eg. https://img1.daumcdn.net/thumb/R640x0/?fname=http://cfile277.uf.daum.net/image/99D447415BA4896424BC9D)
            var i = result.ImageUrl?.LastIndexOf("fname=") ?? -1;
            if (i >= 0)
                result.ImageUrl = result.ImageUrl.Substring(i + "fname=".Length);

            // Protocol sometimes missing
            if (result.ImageUrl?.StartsWith("//") ?? false)
                result.ImageUrl = "https:" + result.ImageUrl;

            return result;
        }
    }
}
