using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LazyMagic
{
    /// <summary>
    /// This static class wraps access to an logger implementing ILogger. 
    /// We use this because this LazyMagic library is called from applications where
    /// using DI is inconvenient or impractical. 
    /// </summary>
    public static class LzLogger
    {
        private static ILogger logger;

        public static void SetLogger(ILogger logger)
        {
            LzLogger.logger = logger;
        }
        public static void Info(string message) => logger.Info(message);
        public static async Task InfoAsync(string message) => await logger.InfoAsync(message);
        public static void Error(Exception ex, string message) => logger.Error(ex, message);
        public static async Task ErrorAsync(Exception ex, string message) => await logger.ErrorAsync(ex, message);

    }
}
