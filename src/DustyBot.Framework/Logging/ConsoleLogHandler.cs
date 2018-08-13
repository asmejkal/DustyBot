using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.IO;
using System.Threading;

namespace DustyBot.Framework.Logging
{
    public class ConsoleLogger : ILogger, IDisposable
    {
        StreamWriter _logWriter;
        SemaphoreSlim _logWriterLock = new SemaphoreSlim(1, 1);
        private DiscordSocketClient _client;

        public ConsoleLogger(DiscordSocketClient client, string logFile = "")
        {
            _client = client;
            _client.Log += Log;

            if (!string.IsNullOrEmpty(logFile))
            {
                FileStream stream;
                if (!File.Exists(logFile))
                    stream = new FileStream(logFile, FileMode.OpenOrCreate);
                else
                    stream = new FileStream(logFile, FileMode.Append);

                _logWriter = new StreamWriter(stream);
            }            
        }

        public async Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString(fullException: false));

            await _logWriterLock.WaitAsync();
            try
            {
                await _logWriter?.WriteLineAsync(DateTime.Now.ToString(@"MM\/dd ") + message.ToString());
            }
            finally
            {
                _logWriterLock.Release();
            }
        }
        
        #region IDisposable 

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logWriter?.Dispose();
                    _logWriter = null;
                    
                    _client.Log -= Log;
                }

                _disposed = true;
            }
        }

        //~()
        //{
        //    Dispose(false);
        //}

        #endregion
    }
}
