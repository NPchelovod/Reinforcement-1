using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    public static class RevitAPI
    {
        public static UIApplication UiApplication { get; set; }
        public static UIDocument UiDocument { get => UiApplication.ActiveUIDocument; }
        public static Document Document { get => UiDocument.Document; }
        public static double ToMm (double number)
        {
           return UnitUtils.ConvertFromInternalUnits(number, UnitTypeId.Millimeters);
        }
        public static double ToFoot(double number)
        {
            return UnitUtils.ConvertToInternalUnits(number, UnitTypeId.Millimeters);
        }
        public static void Initialize(ExternalCommandData commandData)
        {
            UiApplication = commandData.Application;
        }

    }
}
