#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Documents;

#endregion

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class ReinforcementColors : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            // 1. get the active view
            View view = doc.ActiveView;

            using (Transaction t = new Transaction(doc, "Apply view filters"))
            {
                t.Start();
                // 2. create list of parameter filters
                List<ParameterFilterElement> filterElementsList = new List<ParameterFilterElement>();
                 
                var col = new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>();

                int[] idsArray = new int[13] { 3956171, 3956184, 3956173, 3956174, 3956175, 3956176, 3956177, 3956178, 3956179, 3956180, 3956181, 3956182, 3956183 };
                for (int i = 0; i < idsArray.Length; i++)
                {
                    foreach (var parameterFilter in col)
                    {
                        if (parameterFilter.Id.IntegerValue == idsArray[i])
                        {
                            filterElementsList.Add(parameterFilter);
                        }
                    }
                }


                // 3. set graphic overrides
                List<OverrideGraphicSettings> colorOverrideList = new List<OverrideGraphicSettings>();
                for (int i = 0; i < 13; i++)
                {
                    colorOverrideList.Add(new OverrideGraphicSettings());
                }

                colorOverrideList[0].SetProjectionLineColor(new Color(153, 153, 0));
                colorOverrideList[1].SetProjectionLineColor(new Color(0, 127, 255));
                colorOverrideList[2].SetProjectionLineColor(new Color(102, 204, 0));
                colorOverrideList[3].SetProjectionLineColor(new Color(255, 0, 0));
                colorOverrideList[4].SetProjectionLineColor(new Color(255, 127, 127));
                colorOverrideList[5].SetProjectionLineColor(new Color(0, 255, 255));
                colorOverrideList[6].SetProjectionLineColor(new Color(255, 127, 223));
                colorOverrideList[7].SetProjectionLineColor(new Color(159, 127, 255));
                colorOverrideList[8].SetProjectionLineColor(new Color(0, 153, 0));
                colorOverrideList[9].SetProjectionLineColor(new Color(0, 0, 255));
                colorOverrideList[10].SetProjectionLineColor(new Color(255, 127, 0));
                colorOverrideList[11].SetProjectionLineColor(new Color(204, 102, 102));
                colorOverrideList[12].SetProjectionLineColor(new Color(255, 0, 255));


                // 4. apply filter to view and set visibility and overrides

                for (int i = 0; i < filterElementsList.Count; i++)
                {
                    view.AddFilter(filterElementsList[i].Id);
                    view.SetFilterVisibility(filterElementsList[i].Id, true);
                    view.SetFilterOverrides(filterElementsList[i].Id, colorOverrideList[i]);
                }

                t.Commit();
            }

            return Result.Succeeded;
            }
            
        }

        

    }
    

