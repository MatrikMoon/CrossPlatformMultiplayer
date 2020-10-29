using Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace MasterServer
{
    class BGNetLogger : BGNetDebug.ILogger
    {
        public void LogError(string message)
        {
            Logger.Error(message);
        }

        public void LogInfo(string message)
        {
            Logger.Info(message);
        }

        public void LogWarning(string message)
        {
            Logger.Warning(message);
        }
    }
}
