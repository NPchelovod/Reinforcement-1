using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using System;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WixSharp;
using WixSharp.CommonTasks;
using static WixSharp.SetupEventArgs;
using static WixSharp.Win32;





namespace BuildPlugin
{
    internal class Program
    {
        private static string projectName = $"Revit ENS plugin {DateTime.Now.Year}";

        private static string formattedDate = DateTime.Now.ToString("yy.MM.dd.HH");

        private static string version = formattedDate;//"1.2.1";
        static void Main(string[] args)
        {

            // Добавьте этот код ▼
            //if (Compiler.WixLocation == null)
            //{
            //    Compiler.WixLocation = @"C:\Program Files (x86)\WiX Toolset v3.14\bin";
            //    // ИЛИ для последних версий:
            //    // Compiler.WixLocation = @"C:\Program Files\WiX Toolset v3.14\bin";
            //}




            var project = new Project()
            {
                OutFileName = "ENS plugin v." + version,
                Name = projectName, //имя wix проекта
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
                        new File(@"%USERPROFILE%\source\repos\NPchelovod\Reinforcement-1\Reinforcement\Reinforcement.addin"),
                        new Dir(@"ENSPlugin",
                        new DirFiles(@"%USERPROFILE%\source\repos\NPchelovod\Reinforcement-1\Reinforcement\bin\Debug\*.*"))),

                    //!!!!!!!!!!!!! это на работе выше дома!!!!!!!
                    //new InstallDir(@"%AppDataFolder%\Autodesk\Revit\Addins\2024\",
                    //new File(@"%USERPROFILE%\source\repos\Reinforcement-1\Reinforcement\Reinforcement.addin"),
                    //    new Dir(@"ENSPlugin",
                    //    new DirFiles(@"%USERPROFILE%\source\repos\Reinforcement-1\Reinforcement\bin\Debug\*.*"))),

                },
            };
            project.Version = new Version(version);
            project.BuildMsi();
            /*
            projectName = "Revit ENS Plugin 2021";
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
                        new File(@"%USERPROFILE%\source\repos\Vinesence\Reinforcement\Reinforcement\Reinforcement.addin"),
                        new Dir(@"ENSPlugin",
                        new DirFiles(@"%USERPROFILE%\source\repos\Vinesence\Reinforcement\Reinforcement\bin\Debug\*.*")))
                },
            };
            project.Version = new Version(version);
            project.BuildMsi();
            */
        }
    }
}













/*



namespace BuildPlugin
{
    internal class Program
    {
        private static string projectName = "Revit ENS plugin 2024" ;
        private static string version = "1.2.0";
        static void Main(string[] args)
        {

            // Укажите путь к вашей иконке (лучше использовать абсолютный путь)
            string iconPath =   @"%USERPROFILE%\source\repos\Reinforcement-1\BuildPlugin\Resources\ens_icon.ico";
            // USERPROFILE%\source\repos\NPchelovod\Reinforcement-1\BuildPlugin\Resources\ens_icon.ico"
            
            string installDir = @"%USERPROFILE%\AppData\Roaming\Autodesk\Revit\Addins\2024";
            //(@"%AppDataFolder%\Autodesk\Revit\Addins\2024\"

            string addinPath =  @"%USERPROFILE%\source\repos\Reinforcement-1\Reinforcement\Reinforcement.addin";
            //new File(@"%USERPROFILE%\source\repos\NPchelovod\Reinforcement-1\Reinforcement\Reinforcement.addin"),
            string FilesPath = @"%USERPROFILE%\source\repos\Reinforcement-1\Reinforcement\bin\Debug\*.*";
            //@"%USERPROFILE%\source\repos\NPchelovod\Reinforcement-1\Reinforcement\bin\Debug\*.*"

            var contol_path = new List<string>()
            {
                iconPath,installDir, addinPath, FilesPath
            };

            foreach ( var contol in contol_path )
            {
                if (System.IO.File.Exists(contol))
                {
                    Console.WriteLine($"Файл {contol} не найден!");
                };
            };

            var project = new Project()
            {
                OutFileName = "ENS plugin 2024 v." + version,
                Name = projectName,
                UI = WUI.WixUI_ProgressOnly,              
                OutDir = "output",
                GUID = new Guid("{003886B5-89F1-480E-86A2-F93C2D8B07DB}"),
                MajorUpgrade = MajorUpgrade.Default,
                // Современный способ добавления свойств
                Properties = new[]
                {
                    new Property("ARPPRODUCTICON", "ens_icon.ico") // Иконка в Панели управления
                },
                ControlPanelInfo =
                {
                    Manufacturer = Environment.UserName,
                    ProductIcon = "ens_icon.ico"
                },
                Dirs = new Dir[]
                {
                    new InstallDir(installDir,
                        new File(addinPath),
                        new Dir(@"ENSPlugin",
                        new Files(FilesPath),
                        // Явно добавляем иконку в установку
                        new File(iconPath)
                        )),

                },


            };

            // Добавляем иконку в проект (правильный способ)
            project.AddBinary(new Binary(new Id("ENS_Icon"), iconPath));

            // Устанавливаем ссылку на иконку для ARP
            //project.Properties.Add(new Property("ARPPRODUCTICON", "ens_icon.ico"));
            // Устанавливаем иконку для Add/Remove Programs
            // project.AddXml("Wix/Product",
            //@"<Icon Id=""ENS_Icon"" SourceFile=""C:\Users\Pchelovod\source\repos\NPchelovod\Reinforcement-1\BuildPlugin\Resources\ens_icon.ico""/>");
            // Генерируем уникальный GUID для компонента иконки
            // Настройка иконки для ярлыков (если нужно)
            project.WixSourceGenerated += doc =>
            {
                doc.FindAll("Shortcut").ForEach(shortcut =>
                {
                    shortcut.Add(new WixSharp.File("ens_icon.ico"));
                });
            };
            project.Version = new Version(version);

            project.PreserveTempFiles = true;
            project.BuildMsi();
            
        }
    }
}

*/