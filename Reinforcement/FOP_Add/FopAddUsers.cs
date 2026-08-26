
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using View = Autodesk.Revit.DB.View;
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class FopAddUsers : IExternalCommand
    {

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);


            var w = new WPF_FOP(commandData);
            bool? resultW = w.ShowDialog();
            if (resultW != true)
            {
                return Result.Cancelled;
            }
            return Result.Succeeded;
            
        }
    }
}