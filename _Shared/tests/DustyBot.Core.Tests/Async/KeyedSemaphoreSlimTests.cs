using System.Threading.Tasks;
using DustyBot.Core.Async;
using Xunit;

namespace DustyBot.Core.Tests.Async
{
    public class KeyedSemaphoreSlimTests
    {
        [Fact]
        public Task ClaimAsyncMutexTest()
        {
            return TestMutex();
        }

        [Fact]
        public async Task ClaimAsyncSemaphoreTest()
        {
            using var semaphore = new KeyedSemaphoreSlim<string>(2);

            var ta1 = semaphore.ClaimAsync("A");
            var ta2 = semaphore.ClaimAsync("A");
            var ta3 = semaphore.ClaimAsync("A");
            var tb1 = semaphore.ClaimAsync("B");

            Assert.True(ta1.IsCompleted);
            Assert.True(ta2.IsCompleted);
            Assert.False(ta3.IsCompleted);
            Assert.True(tb1.IsCompleted);

            // Complete the first entry
            var ra1 = await ta1;
            ra1.Dispose();
            await Task.Yield();

            Assert.True(ta3.IsCompleted);
        }

        [Fact]
        public Task ClaimAsyncWithoutPoolTest()
        {
            return TestMutex(0);
        }

        [Fact]
        public Task ClaimAsyncSinglePoolTest()
        {
            return TestMutex(1);
        }

        [Fact]
        public Task ClaimAsyncDoublePoolTest()
        {
            return TestMutex(2);
        }

        [Fact]
        public Task ClaimAsyncInfinitePoolTest()
        {
            return TestMutex(int.MaxValue);
        }

        private async Task TestMutex(int? maxPoolSize = null)
        {
            KeyedSemaphoreSlim<string> semaphore;
            if (maxPoolSize.HasValue)
                semaphore = new KeyedSemaphoreSlim<string>(1, maxPoolSize.Value);
            else
                semaphore = new KeyedSemaphoreSlim<string>(1);

            using (semaphore)
            {
                // Use three keys to create three semaphores.
                var ta1 = semaphore.ClaimAsync("A");
                var tb1 = semaphore.ClaimAsync("B");
                var ta2 = semaphore.ClaimAsync("A");
                var tb2 = semaphore.ClaimAsync("B");
                var ta3 = semaphore.ClaimAsync("A");
                var tc1 = semaphore.ClaimAsync("C");

                // Assert that first entry for each key is complete.
                Assert.True(ta1.IsCompleted);
                Assert.True(tb1.IsCompleted);
                Assert.False(ta2.IsCompleted);
                Assert.False(tb2.IsCompleted);
                Assert.False(ta3.IsCompleted);
                Assert.True(tc1.IsCompleted);

                // Complete the first entry by disposing.
                var ra1 = await ta1;
                ra1.Dispose();
                var rb1 = await tb1;
                rb1.Dispose();
                var rc1 = await tc1;
                rc1.Dispose();
                await TaskHelper.YieldMany(100);

                // Show that the second entry is now complete.
                Assert.True(ta2.IsCompleted);
                Assert.True(tb2.IsCompleted);
                Assert.False(ta3.IsCompleted);

                // Complete the second entry by disposing.
                var ra2 = await ta2;
                ra2.Dispose();
                var rb2 = await tb2;
                rb2.Dispose();
                await TaskHelper.YieldMany(100);

                // Show that the third entry is now complete.
                Assert.True(ta3.IsCompleted);

                // Complete the third entry by disposing.
                var ra3 = await ta3;
                ra3.Dispose();
                await TaskHelper.YieldMany(100);

                // Assert that each key shares a unique scope instance.
                Assert.Same(ra1, ra2);
                Assert.Same(ra2, ra3);
                Assert.Same(rb1, rb2);
                Assert.NotSame(ra1, rb1);
                Assert.NotSame(ra1, rc1);

                // Get four new keys.
                var td1 = semaphore.ClaimAsync("D");
                var te1 = semaphore.ClaimAsync("E");
                var tf1 = semaphore.ClaimAsync("F");
                var tg1 = semaphore.ClaimAsync("G");

                // Assert that they are all complete.
                Assert.True(td1.IsCompleted);
                Assert.True(te1.IsCompleted);
                Assert.True(tf1.IsCompleted);
                Assert.True(tg1.IsCompleted);

                // Complete the first = entry for each.
                var rd1 = await td1;
                rd1.Dispose();
                var re1 = await te1;
                re1.Dispose();
                var rf1 = await tf1;
                rf1.Dispose();
                var rg1 = await tg1;
                rg1.Dispose();
                await TaskHelper.YieldMany(100);

                if (maxPoolSize >= 3)
                {
                    Assert.Same(rc1, rd1);
                    Assert.Same(rb2, re1);
                    Assert.Same(ra1, rf1);
                }
                else if (maxPoolSize == 2)
                {
                    Assert.Same(rc1, rd1);
                    Assert.Same(rb2, re1);
                    Assert.NotSame(ra1, rf1);
                }
                else if (maxPoolSize == 1)
                {
                    Assert.Same(rc1, rd1);
                    Assert.NotSame(rb2, re1);
                    Assert.NotSame(ra1, rf1);
                }
                else if (maxPoolSize == 0)
                {
                    Assert.NotSame(rc1, rd1);
                    Assert.NotSame(rb2, re1);
                    Assert.NotSame(ra1, rf1);
                }
            }
        }
    }
}
