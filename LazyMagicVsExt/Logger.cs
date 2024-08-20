using System;
using System.Threading.Tasks;
using LazyMagic;

namespace LazyMagicVsExt
{

    class Logger : ILogger
    {
        public Logger( IProgress<LogEntry> progress)
        {
            this.progress = progress;
        }

        private readonly IProgress<LogEntry> progress;
        private int index;

        public void Info(string message)
        {
            progress.Report(new LogEntry() { DateTime = DateTime.Now, Index = index++, Message = message });
        }

        public async Task InfoAsync(string message)
        {
            await Task.Delay(0);
            progress.Report(new LogEntry() { DateTime = DateTime.Now, Index = index++, Message = message });
        }


        public void Error(Exception ex, string message)
        {
            progress.Report(new LogEntry() { DateTime = DateTime.Now, Index = index++, Message = message + "\n" + ex.Message }); ;
        }

        public async Task ErrorAsync(Exception ex, string message)
        {
            await Task.Delay(0);
            progress.Report(new LogEntry() { DateTime = DateTime.Now, Index = index++, Message = message + "\n" + ex.Message }); ;
        }

    }
}
