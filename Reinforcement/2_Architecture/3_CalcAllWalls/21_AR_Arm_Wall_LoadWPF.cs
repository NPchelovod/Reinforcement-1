using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class AR_Arm_Wall_LoadWPF : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

           
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Succeeded;
            Document doc = uidoc.Document;
            var viewModel = new CalculateArmWalls(doc);

            viewModel.ShowDialog();
            return Result.Succeeded;

        }
    }
}