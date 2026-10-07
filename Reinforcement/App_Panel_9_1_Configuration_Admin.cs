using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;




namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class App_Panel_9_1_Configuration_Admin : IExternalCommand
    {
        public Result Execute(
             ExternalCommandData commandData,
             ref string message,
             ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            // панели которые должны быть видны
            //проверяем пароль
            if (!Statist.RequestPassword())
            {
                message = "Неверный пароль или отмена.";
                return Result.Cancelled;
            }
            App_Helper_Panels.CreatePanelConfiguration(EPanelSelf.Admin);


            return Result.Succeeded;
        }
    }
}