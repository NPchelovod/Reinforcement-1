using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class OpenFamiliesFolder : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // Укажите путь к папке, которую вы хотите открыть
            string folderPath = @"Y:\Revit\_Семейства";
            string arguments = "\"" + folderPath + "\"";
            try
            {
                // Открываем папку в проводнике
                Process.Start("explorer.exe", arguments);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // В случае ошибки выводим сообщение
                MessageBox.Show("Не удалось открыть папку: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}
