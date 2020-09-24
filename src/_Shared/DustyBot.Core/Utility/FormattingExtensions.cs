namespace DustyBot.Core.Utility
{
    public static class FormattingExtensions
    {
        public static string ToStringBigNumber(this int num)
        {
            double numStr;
            string suffix;
            if (num < 1000d)
            {
                numStr = num;
                suffix = "";
            }
            else if (num < 1000000d)
            {
                numStr = num / 1000d;
                suffix = "K";
            }
            else if (num < 1000000000d)
            {
                numStr = num / 1000000d;
                suffix = "M";
            }
            else
            {
                numStr = num / 1000000000d;
                suffix = "B";
            }
            return numStr.ToString("#.##") + suffix;
        }
    }
}
