using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;



using System.Linq;
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class App_Panel_1_1_Configuration_KR : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            // панели которые должны быть видны
            App_Helper_Panels.CreatePanelConfiguration(EPanelSelf.KR);
           

            return Result.Succeeded;
        }
    }
    
}
