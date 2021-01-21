using System;
using System.Threading.Tasks;

namespace DustyBot.Core.Async
{
    public static class TaskHelper
    {
        private static readonly Action<Task> DefaultErrorContinuation =
        t =>
        {
            try 
            { 
                t.Wait(); 
            }
            catch 
            {
            }
        };

        public static void FireForget(Func<Task> action, Action<Exception> handler = null)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var task = Task.Run(action);

            if (handler == null)
            {
                task.ContinueWith(
                    DefaultErrorContinuation,
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
            }
            else
            {
                task.ContinueWith(
                    t => handler(t.Exception.GetBaseException()),
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public static async Task YieldMany(int count)
        {
            for (int i = 0; i < count; ++i)
                await Task.Yield();
        }
    }
}
