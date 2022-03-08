using DustyBot.DaumCafe.Utility;
using HtmlAgilityPack;

namespace DustyBot.DaumCafe
{
    public class DaumCafePageBody
    {
        public string? Subject { get; private set; }
        public string? Text { get; private set; }
        public string? ImageUrl { get; private set; }

        public DaumCafePageBody(string? subject, string? text, string? imageUrl)
        {
            Subject = subject;
            Text = text;
            ImageUrl = imageUrl;
        }

        public static DaumCafePageBody Create(string content)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var subject = doc.DocumentNode.Descendants("h3").FirstOrDefault(x => x.GetAttributeValue("class", "") == "tit_subject")?.InnerText.Trim();

            var text = doc.DocumentNode.Descendants("div").FirstOrDefault(x => x.GetAttributeValue("id", "") == "article");
            if (text != null)
            {
                var imageUrl = text.Descendants("img").FirstOrDefault(x => x.Attributes.Contains("src"))?.GetAttributeValue("src", "").Trim();
                var plainText = text.ToPlainText().Trim();
                return new DaumCafePageBody(subject, plainText, imageUrl);
            }

            return new DaumCafePageBody(subject, null, null);
        }

        public static DaumCafePageBody CreateFromComment(string content)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var text = doc.DocumentNode.Descendants("span").FirstOrDefault(x => x.HasClass("txt_detail"))?.ToPlainText().Trim();
            var imageUrl = doc.DocumentNode.Descendants("img").FirstOrDefault(x => x.HasClass("thumb_info"))?.GetAttributeValue("src", null)?.Trim().Replace("C120x120", "R640x0");

            // Discord stopped embedding the scaled down links (eg. https://img1.daumcdn.net/thumb/R640x0/?fname=http://cfile277.uf.daum.net/image/99D447415BA4896424BC9D)
            var i = imageUrl?.LastIndexOf("fname=") ?? -1;
            if (i >= 0)
                imageUrl = imageUrl!.Substring(i + "fname=".Length);

            // Protocol sometimes missing
            if (imageUrl?.StartsWith("//") ?? false)
                imageUrl = "https:" + imageUrl;

            return new DaumCafePageBody(null, text, imageUrl);
        }
    }
}
