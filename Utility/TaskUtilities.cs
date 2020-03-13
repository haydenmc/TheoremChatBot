using System;
using System.Threading.Tasks;

namespace Theorem.Utility
{
    public static class TaskUtilities
    {
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
    }
}