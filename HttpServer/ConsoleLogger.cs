using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DF_FaceTracking.cs.HttpServer
{
    class ConsoleLogger : ILogger
    {
        public void Log(object message)
        {
            Console.WriteLine(message);
        }
    }
}
