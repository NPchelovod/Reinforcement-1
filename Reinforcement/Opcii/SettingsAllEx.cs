using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class SettingsAllEx : IExternalCommand
    {
        public Result Execute(
           ExternalCommandData commandData,
           ref string message,
           ElementSet elements)
        {
            var SettingsWindow = new SettingsWindow();
            bool? resultW = SettingsWindow.ShowDialog();
            if (resultW != true)
            {
                return Result.Cancelled;
            }
            return Result.Succeeded;
        }
    }
}