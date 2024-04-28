using System;
using System.Collections.Generic;
using System.Threading;

namespace OrderItemsReserver;
public static class Retry
{
    public static void Do(Action action, TimeSpan retryInterval, int maxAttemptCount = 3)
    {
        var exceptions = new List<Exception>();
        for (int attempted = 0; attempted < maxAttemptCount; attempted++)
        {
            try
            {
                if (attempted > 0)
                    Thread.Sleep(retryInterval);
                
                action();
                return;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }
        throw new AggregateException(exceptions);
    }
}
