using System;
using System.Threading.Tasks;
using DustyBot.Core.Miscellaneous;
using DustyBot.Database.Mongo.Collections;
using Moq;
using Xunit;

namespace DustyBot.Database.Services.Tests
{

    public class ProxyListServiceTests
    {
        [Fact]
        public async Task BlacklistProxyAsyncTest()
        {
            var proxyAddress = "http://127.0.0.1:1234";
            var blacklistDuration = TimeSpan.FromHours(1);
            var proxyList = new ProxyList();

            var settingsService = new Mock<ISettingsService>();
            settingsService.Setup(x => x.ModifyGlobal(It.IsAny<Action<ProxyList>>()))
                .Callback<Action<ProxyList>>(x => x(proxyList))
                .Returns(Task.CompletedTask);

            var now = DateTimeOffset.Now;
            var timeProvider = new Mock<ITimeProvider>();
            timeProvider.Setup(x => x.Now)
                .Returns(now);

            timeProvider.Setup(x => x.UtcNow)
                .Returns(now.ToUniversalTime());

            var instance = new ProxyListService(settingsService.Object, timeProvider.Object);
            await instance.BlacklistProxyAsync(proxyAddress, blacklistDuration);
            Assert.Collection(proxyList.Blacklist, x => 
            {
                Assert.Equal(proxyAddress, x.Key);
                Assert.Equal(proxyAddress, x.Value.Address);
                Assert.Equal(now + blacklistDuration, x.Value.Expiration);
            });
        }
    }
}
