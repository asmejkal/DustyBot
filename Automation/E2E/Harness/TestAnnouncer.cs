using System.Reflection;
using System.Threading.Tasks;
using Disqord;
using Xunit.Abstractions;

namespace DustyBot.Automation.E2E.Harness
{
    /// <summary>
    /// Posts a distinct embed announcing the current test's name before it runs, so a human reviewing the
    /// channel history in Discord afterwards can see where each test's messages start.
    /// </summary>
    public static class TestAnnouncer
    {
        private static readonly Color AnnouncementColor = new(0x5865F2); // Discord blurple

        public static Task AnnounceAsync(TestDiscordClient announcer, Snowflake channelId, ITestOutputHelper output)
        {
            var embed = new LocalEmbed()
                .WithColor(AnnouncementColor)
                .WithTitle("▶ " + GetTestName(output));

            return announcer.SendEmbedAsync(channelId, embed);
        }

        // xUnit doesn't officially expose the running test's name to the test itself; ITestOutputHelper's
        // concrete implementation carries it in a private field. This is a well-known, widely used workaround.
        // Built from the class/method info directly (rather than ITest.DisplayName) so Theory parameters and
        // the namespace are left out - just "ClassName.MethodName".
        private static string GetTestName(ITestOutputHelper output)
        {
            var field = output.GetType().GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
            var test = (ITest)field!.GetValue(output)!;
            var testMethod = test.TestCase.TestMethod;

            var className = testMethod.TestClass.Class.Name;
            var simpleClassName = className.Substring(className.LastIndexOf('.') + 1);

            return $"{simpleClassName}.{testMethod.Method.Name}";
        }
    }
}
