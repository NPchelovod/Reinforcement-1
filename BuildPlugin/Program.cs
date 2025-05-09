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
        private static string projectName = "Revit ENS plugin 2024" ;
        private static string version = "1.2.1";
        static void Main(string[] args)
        {

            // Укажите путь к вашей иконке (лучше использовать абсолютный путь)
            string iconPath = @"%USERPROFILE%\source\repos\NPchelovod\Reinforcement-1\BuildPlugin\Resources\ens_icon.ico";
           
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
                    new InstallDir(@"%AppDataFolder%\Autodesk\Revit\Addins\2024\",
                        new File(@"%USERPROFILE%\source\repos\NPchelovod\Reinforcement-1\Reinforcement\Reinforcement.addin"),
                        new Dir(@"ENSPlugin",
                        new Files(@"%USERPROFILE%\source\repos\NPchelovod\Reinforcement-1\Reinforcement\bin\Debug\*.*"),
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
