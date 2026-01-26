using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Updaters;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    
        public class PropertiesMask : IExternalCommand
        {
            public Result Execute(
                ExternalCommandData commandData,
                ref string message,
                ElementSet elements)
            {
                //Utilit_1_1_Depth_Seach.ResetNamesParam=true;
                ChangeWidthAnnotationTag.ChangeMaskField = !ChangeWidthAnnotationTag.ChangeMaskField;
                return Result.Succeeded;
            }
        }
    }

