using System;
using System.Configuration;
using System.IO;
using System.Reflection;
namespace Reinforcement
{
    internal static class PluginOptions
    {
        public static bool AutomaticUpdatesEnabled
        {
            get
            {
                string path = Assembly.GetExecutingAssembly().Location + ".config";
                if (!File.Exists(path)) return true;   // ← было false
                try
                {
                    var map = new ExeConfigurationFileMap { ExeConfigFilename = path };
                    var configuration = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);

                    string value = configuration.AppSettings.Settings["EnableAutomaticUpdates"]?.Value;

                    // Если ключа нет — считаем, что обновление разрешено.
                    if (value == null) return true;   // ← добавить

                    return string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
                }
                catch (ConfigurationErrorsException) { return true; } // ← было false
            }
        }
        //public static bool AutomaticUpdatesEnabled
        //{
        //    get
        //    {
        //        string path = Assembly.GetExecutingAssembly().Location + ".config";
        //        if (!File.Exists(path)) return false;
        //        try
        //        {
        //            var map = new ExeConfigurationFileMap { ExeConfigFilename = path };
        //            var configuration = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
        //            return string.Equals(configuration.AppSettings.Settings["EnableAutomaticUpdates"]?.Value,
        //                "true", StringComparison.OrdinalIgnoreCase);
        //        }
        //        catch (ConfigurationErrorsException) { return false; }
        //    }
        //}
    }
}
