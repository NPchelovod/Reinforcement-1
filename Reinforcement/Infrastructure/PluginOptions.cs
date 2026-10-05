using System;
using System.Configuration;
using System.IO;
using System.Reflection;
namespace Reinforcement
{
    internal static class PluginOptions
    {
        public static bool AutomaticUpdatesEnabled => true;
        public static bool AutomaticUpdatesEnabled2
        {
            get
            {
                string path = Assembly.GetExecutingAssembly().Location + ".config";
                if (!File.Exists(path)) return false;
                try
                {
                    var map = new ExeConfigurationFileMap { ExeConfigFilename = path };
                    var configuration = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
                    return string.Equals(configuration.AppSettings.Settings["EnableAutomaticUpdates"]?.Value,
                        "true", StringComparison.OrdinalIgnoreCase);
                }
                catch (ConfigurationErrorsException) { return false; }
            }
        }
    }
}
