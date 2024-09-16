using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CreateSheet : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            try
            {
                using (Transaction t = new Transaction(doc, "Создание листа"))
                {
                    t.Start();
                    //Get titleblock
                    var colTitleBlocks = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).OfClass(typeof(FamilySymbol));

                    //Get viewPlans from "CreateViewPlan"

                    //Get viewport labels
                    var colViewTitles = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ViewportLabel).OfClass(typeof(FamilySymbol));

                    //Get viewports
                    var colViewPorts = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Viewports).OfClass(typeof(FamilySymbol));

                    //Create an empty sheet
                    ElementId titleBlockTypeId = new ElementId(218938);
                    var newSheet = ViewSheet.Create(doc, titleBlockTypeId);


                    RevitAPI.ToFoot(300);
                    

                    t.Commit();

                   

                }
            }
            catch (Exception ex)
            {
                //Код в случае ошибки
                MessageBox.Show("Чет пошло не так!\n" + ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }
    }
}
