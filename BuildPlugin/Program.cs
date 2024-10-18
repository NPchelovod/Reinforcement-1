using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WixSharp;
using static WixSharp.SetupEventArgs;
using static WixSharp.Win32;

namespace BuildPlugin
{
    internal class Program
    {
        private static string projectName = "Revit ENS plugin";
        private static string version = "1.0";
        static void Main(string[] args)
        {
            var project = new Project()
            {
                OutFileName = "ENS plugin 2024",
                Name = projectName,
                UI = WUI.WixUI_ProgressOnly,
                OutDir = "output",
                GUID = new Guid("{003886B5-89F1-480E-86A2-F93C2D8B07DB}"),
                MajorUpgrade = MajorUpgrade.Default,
                ControlPanelInfo =
                {
                    Manufacturer = Environment.UserName,
                },
                Dirs = new Dir[]
                {
                    new InstallDir(@"%AppDataFolder%\Autodesk\Revit\Addins\2024\",
                        new File(@"C:\Users\KMedvedev\source\repos\Reinforcement\Reinforcement\Reinforcement.addin"),
                        new Dir(@"ENSPlugin",
                        new DirFiles(@"C:\Users\KMedvedev\source\repos\Reinforcement\Reinforcement\bin\Debug\*.*"))),

                },
            };
            project.Version = new Version(version);
            project.BuildMsi();

            project = new Project()
            {
                OutFileName = "ENS plugin 2021",
                Name = projectName,
                UI = WUI.WixUI_ProgressOnly,
                OutDir = "output",
                GUID = new Guid("{D9449A17-6853-4EDF-8093-E8E9EC6EC084}"),
                MajorUpgrade = MajorUpgrade.Default,
                ControlPanelInfo =
                {
                    Manufacturer = Environment.UserName,
                },
                Dirs = new Dir[]
                {
                    new InstallDir(@"%AppDataFolder%\Autodesk\Revit\Addins\2021\",
                        new File(@"C:\Users\KMedvedev\source\repos\Reinforcement\Reinforcement\Reinforcement.addin"),
                        new Dir(@"ENSPlugin",
                        new DirFiles(@"C:\Users\KMedvedev\source\repos\Reinforcement\Reinforcement\bin\Debug\*.*")))
                },
            };
            project.Version = new Version(version);
            project.BuildMsi();
        }
    }
}
