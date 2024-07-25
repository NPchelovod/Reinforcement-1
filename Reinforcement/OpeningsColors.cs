#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows.Documents;

#endregion

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class OpeningsColors : IExternalCommand
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

                int[] idsArray = new int[8] { 10119785, 10015139, 6364113, 10013253, 6364114, 10119788, 6364116, 6364115 };
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
                for (int i = 0; i < 8; i++)
                {
                    colorOverrideList.Add(new OverrideGraphicSettings());
                }

                colorOverrideList[0].SetProjectionLineColor(new Color(255, 128, 000));
                colorOverrideList[1].SetProjectionLineColor(new Color(0, 0, 255));
                colorOverrideList[2].SetProjectionLineColor(new Color(0, 127, 0));
                colorOverrideList[3].SetProjectionLineColor(new Color(191, 0, 255));
                colorOverrideList[4].SetProjectionLineColor(new Color(165, 165, 0));
                colorOverrideList[5].SetProjectionLineColor(new Color(127, 191, 255));
                colorOverrideList[6].SetProjectionLineColor(new Color(0, 255, 128));
                colorOverrideList[7].SetProjectionLineColor(new Color(255, 63, 0));
               


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
    

