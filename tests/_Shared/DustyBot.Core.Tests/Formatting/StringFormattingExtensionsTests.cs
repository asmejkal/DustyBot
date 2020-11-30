using System;
using DustyBot.Core.Formatting;
using Xunit;

namespace DustyBot.Core.Tests.Formatting
{
    public class StringFormattingExtensionsTests
    {
        [Fact]
        public void TrimTest()
        {
            Assert.Equal("asd", "*asd*".Trim('*', 1));
            Assert.Equal("*asd*", "**asd**".Trim('*', 1));
            Assert.Equal("asd", "**asd**".Trim('*', 2));
            Assert.Equal("'asd'", "*'asd'*".Trim('*', 2));
            Assert.Equal("*asd", "**asd".Trim('*', 1));
            Assert.Equal("asd*", "asd**".Trim('*', 1));
            Assert.Equal("", "*".Trim('*', 1));
            Assert.Equal("", "**".Trim('*', 1));
            Assert.Equal("*", "***".Trim('*', 1));
            Assert.Equal("", "*".Trim('*', 2));
            Assert.Equal("", "****".Trim('*', 2));
            Assert.Equal("**", "******".Trim('*', 2));
        }
    }
}
