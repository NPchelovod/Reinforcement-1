using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class App_Panel_4_1_Configuration_VK : IExternalCommand
    {
        public Result Execute(
             ExternalCommandData commandData,
             ref string message,
             ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            // панели которые должны быть видны
            App_Helper_Panels.CreatePanelConfiguration(EPanelSelf.VK);


            return Result.Succeeded;
        }
    }
}