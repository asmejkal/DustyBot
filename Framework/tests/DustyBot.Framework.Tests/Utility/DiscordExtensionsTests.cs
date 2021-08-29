using System;
using Discord;
using DustyBot.Framework.Utility;
using Moq;
using Xunit;

namespace DustyBot.Framework.Tests.Utility
{
    public class DiscordExtensionsTests
    {
        [Fact]
        public void GetRolesTest()
        {
            var roles = new[]
            {
                new Mock<IRole>().Object,
                new Mock<IRole>().Object,
                new Mock<IRole>().Object
            };

            var guild = new Mock<IGuild>();
            guild.Setup(x => x.GetRole(1)).Returns(roles[0]);
            guild.Setup(x => x.GetRole(2)).Returns(roles[1]);
            guild.Setup(x => x.GetRole(3)).Returns(roles[2]);
            guild.Setup(x => x.GetRole(4)).Returns((IRole)null);

            var user = new Mock<IGuildUser>();
            user.SetupGet(x => x.Guild).Returns(guild.Object);
            user.SetupGet(x => x.RoleIds).Returns(new[] { 1UL, 2UL, 3UL, 4UL });

            var result = user.Object.GetRoles();
            Assert.Equal(roles, result);
        }

        [Fact]
        public void GetTopRoleTest()
        {
            var role1 = new Mock<IRole>();
            role1.SetupGet(x => x.Position).Returns(1);

            var role2 = new Mock<IRole>();
            role2.SetupGet(x => x.Position).Returns(3);

            var role3 = new Mock<IRole>();
            role3.SetupGet(x => x.Position).Returns(2);

            var guild = new Mock<IGuild>();
            guild.Setup(x => x.GetRole(1)).Returns(role1.Object);
            guild.Setup(x => x.GetRole(2)).Returns(role2.Object);
            guild.Setup(x => x.GetRole(3)).Returns(role3.Object);

            var user = new Mock<IGuildUser>();
            user.SetupGet(x => x.Guild).Returns(guild.Object);
            user.SetupGet(x => x.RoleIds).Returns(new[] { 1UL, 2UL, 3UL });

            Assert.Same(role2.Object, user.Object.GetTopRole());
        }

        [Fact]
        public void GetTopRoleEmptyTest()
        {
            var guild = new Mock<IGuild>();

            var user = new Mock<IGuildUser>();
            user.SetupGet(x => x.Guild).Returns(guild.Object);
            user.SetupGet(x => x.RoleIds).Returns(Array.Empty<ulong>());

            Assert.Null(user.Object.GetTopRole());
        }
    }
}
