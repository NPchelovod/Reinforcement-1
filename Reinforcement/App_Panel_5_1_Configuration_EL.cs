using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;



namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class App_Panel_5_1_Configuration_EL : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            // панели которые должны быть видны
            App_Helper_Panels.CreatePanelConfiguration(EPanelSelf.EL);


            return Result.Succeeded;
        }
    }
}