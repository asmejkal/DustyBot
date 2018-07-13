using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.IO;

namespace DustyBot.Framework.Logging
{
    public class ConsoleLogger : ILogger, IDisposable
    {
        public const string LogFile = "log.txt";

        StreamWriter _logWriter;
        private DiscordSocketClient _client;

        public ConsoleLogger(DiscordSocketClient client, bool backupToFile = true)
        {
            _client = client;
            _client.Log += Log;

            FileStream stream;
            if (!File.Exists(LogFile))
                stream = new FileStream(LogFile, FileMode.OpenOrCreate);
            else
                stream = new FileStream(LogFile, FileMode.Append);

            _logWriter = new StreamWriter(stream);
        }

        public async Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString(fullException: false));
            await _logWriter?.WriteLineAsync(DateTime.Now.ToString("MM//dd ") + message.ToString());
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
