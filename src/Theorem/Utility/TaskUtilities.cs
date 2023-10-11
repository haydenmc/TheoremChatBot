using System;
using System.Threading;
using System.Threading.Tasks;

namespace Theorem.Utility
{
    public static class TaskUtilities
    {
        /// <summary>
        /// This helps squelch a warning that occurs when you discard a Task that
        /// is returned from an async method.
        /// </summary>
        public static async void FireAndForget(this Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Unhandled exception in a fire-and-forget async task:\n" + 
                    $"{e.ToString()}");
            }
        }

        /// <summary>
        /// Runs an async action and re-runs it when it throws an Exception.
        /// Delay between re-runs is exponential up to the specified maximum.
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <param name="onException">Callback that is invoked when an exception is thrown</param>
        /// <param name="maxRetries">Max number of retries to attempt</param>
        /// <param name="nextRetrySeconds">The maximum amount of time to wait between retries</param>
        /// <returns></returns>
        public async static Task ExpontentialRetryAsync(
            Func<Task> action,
            Action<Exception, (uint retryNumber, uint nextRetrySeconds)> onException,
            uint maxRetries = 0,
            uint maxRetryDelaySeconds = 120)
        {
            uint currentRetry = 0;
            DateTime lastRun;
            while (true)
            {
                lastRun = DateTime.Now;
                Exception exception = null;
                try
                {
                    await action();
                }
                catch (Exception e)
                {
                    exception = e;
                }
                // If the action has managed to run for more than a minimal amount
                // of time, reset the retry count.
                TimeSpan runTime = DateTime.Now.Subtract(lastRun);
                if (runTime.TotalSeconds > 10)
                {
                    currentRetry = 0;
                }
                uint delaySeconds = (uint)Math.Pow(2, currentRetry);
                if (maxRetryDelaySeconds != 0 && (delaySeconds > maxRetryDelaySeconds))
                {
                    delaySeconds = maxRetryDelaySeconds;
                }
                if (exception != null)
                {
                    onException(exception, (currentRetry, delaySeconds));
                }
                currentRetry++;
                Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }
}