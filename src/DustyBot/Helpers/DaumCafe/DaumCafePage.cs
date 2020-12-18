namespace DustyBot.Helpers.DaumCafe
{
    public class DaumCafePage
    {
        public string RelativeUrl { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string ImageUrl { get; set; }
        public string Description { get; set; }

        public DaumCafePageBody Body { get; set; }
    }
}
