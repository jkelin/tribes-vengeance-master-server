using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TribesVengeanceMasterServer
{
    public static class TaskPool
    {
        private static readonly ConcurrentDictionary<Guid, Task> Tasks = new ConcurrentDictionary<Guid, Task>();

        public static void Run(Task task)
        {
            var id = Guid.NewGuid();
            Tasks.TryAdd(id, task);
        }

        private static async Task Run(Guid id, Task task)
        {
            try
            {
                await task;
            }
            finally
            {
                Tasks.TryRemove(id, out var _);
            }
        }
    }
}
