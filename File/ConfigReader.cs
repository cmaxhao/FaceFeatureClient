using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

namespace DF_FaceTracking.cs.File
{
    class ConfigReader
    {
        private static Configuration config = null;
        public static String GetConfigValue(String key)
        {
            if(config == null)
            {
                string file = System.Windows.Forms.Application.ExecutablePath;
                config = ConfigurationManager.OpenExeConfiguration(file);
            }
            return config.AppSettings.Settings[key].Value.ToString();
        }
    }
}
