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
using System.Net.NetworkInformation;
using System.Windows.Documents;
using form = System.Windows.Forms;
#endregion

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class ActivateFiltersByName : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            // Искомый фрагмент имени фильтра
            string filterNamePart = "RC - D";

            // Собираем все фильтры параметров в документе
            List<ParameterFilterElement> matchingFilters = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .Where(f => f.Name.Contains(filterNamePart))
                .ToList();

            if (matchingFilters.Count == 0)
            {


                // 2. Диалог: применять ли цветовые переопределения?
                TaskDialog dialog = new TaskDialog($"Фильтры {filterNamePart} не найдены, переопределить цвета?");
                dialog.MainInstruction = "Использовать простое переопределение цветов арматуры?";
                dialog.MainContent = "Если 'Да', к найденным фильтрам будут применены заранее заданные цвета. Если 'Нет', фильтры будут просто включены без изменения графики.";
                dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                TaskDialogResult result = dialog.Show();

                bool applyColors = (result == TaskDialogResult.Yes);
                if (!applyColors)
                {
                    return Result.Cancelled;
                }
                var reinforcementColors = new ReinforcementColors();
                return reinforcementColors.Execute(commandData, ref message, elements);
            }
            View targetView = activeView;//
            //если наложен шаблон вида
            if (activeView.ViewTemplateId != ElementId.InvalidElementId)
            {
                View viewTemplate = doc.GetElement(activeView.ViewTemplateId) as View;
                if (viewTemplate != null)
                {
                    targetView = viewTemplate;
                }
            }
            using (Transaction t = new Transaction(doc, "Активация фильтров вида"))
            {
                t.Start();

                foreach (ParameterFilterElement filter in matchingFilters)
                {
                    ElementId filterId = filter.Id;

                    // Добавляем фильтр к виду, если его ещё нет
                    if (!targetView.GetFilters().Contains(filterId))
                    {
                        targetView.AddFilter(filterId);
                    }
                    // Включаем видимость фильтра
                    targetView.SetFilterVisibility(filterId, true);

                    string name = filter.Name;
                    //ищем совпадающую настройку
                    var settings = GetColorsArm.Where(x=>name.Contains(x.Ds));
                    if(settings.Any())
                    {
                        var set = settings.First();
                        //надо настроить в Линии (Проекции/Поверхности) у фильтра свойство данной линии
                        // Применяем переопределение графики (цвет линий и т.д.)
                        targetView.SetFilterOverrides(filterId, set.OverrideGraphicSettings);
                        //Если нужно также настроить цвет поверхности (штриховки), добавьте в инициализацию OverrideGraphicSettings соответствующие вызовы
                    }


                    // (Опционально) Сбросить переопределения графики, если нужно убрать старую раскраску
                    // view.SetFilterOverrides(filterId, new OverrideGraphicSettings());
                }

                t.Commit();
            }

            return Result.Succeeded;
        }

        public static List<(string Ds, int Di,int Id, Color Color, OverrideGraphicSettings OverrideGraphicSettings)> GetColorsArm = new List<(string Ds, int Di, int Id, Color Color, OverrideGraphicSettings OverrideGraphicSettings)>
        {
            ("D6", 6,3956171, new Color(153, 153, 0),new OverrideGraphicSettings().SetProjectionLineColor(new Color(153, 153, 0) )),
            ("D8", 8, 3956184, new Color(0, 127, 255), new OverrideGraphicSettings().SetProjectionLineColor(new Color(0, 127, 255))),
            ("D10", 10, 3956173, new Color(153, 153, 0), new OverrideGraphicSettings().SetProjectionLineColor(new  Color(102, 204, 0))),
            ("D12", 12, 3956174, new Color(153, 153, 0), new OverrideGraphicSettings().SetProjectionLineColor(new  Color(255, 0, 0))),
            ("D14", 14, 3956175, new Color(153, 153, 0), new OverrideGraphicSettings().SetProjectionLineColor(new  Color(255, 127, 127))),
            ("D16", 16, 3956176, new Color(153, 153, 0), new OverrideGraphicSettings().SetProjectionLineColor(new  Color(0, 255, 255))),
            ("D18", 18, 3956177, new Color(153, 153, 0), new OverrideGraphicSettings().SetProjectionLineColor(new  Color(255, 127, 223))),
            ("D20", 20, 3956178, new Color(153, 153, 0), new OverrideGraphicSettings().SetProjectionLineColor(new  Color(159, 127, 255))),
            ("D22", 22, 3956179, new Color(153, 153, 0), new OverrideGraphicSettings().SetProjectionLineColor(new  Color(0, 153, 0))),
            ("D25", 25, 3956180,new Color(153, 153, 0),new OverrideGraphicSettings().SetProjectionLineColor(new  Color(0, 0, 255))),
            ("D28", 28, 3956181,new Color(153, 153, 0),new OverrideGraphicSettings().SetProjectionLineColor(new  Color(255, 127, 0))),
            ("D32", 32,3956182, new Color(153, 153, 0),new OverrideGraphicSettings().SetProjectionLineColor(new  Color(204, 102, 102))),
            ("D36", 36,3956183, new Color(153, 153, 0),new OverrideGraphicSettings().SetProjectionLineColor(new  Color(255, 0, 255))),
        };
    }

    
    

    [Transaction(TransactionMode.Manual)]
    public class ReinforcementColors : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            
            Document doc = uidoc.Document;

            // 1. get the active view
            View activeView = doc.ActiveView;
            View targetView = activeView;//
            //если наложен шаблон вида
            if (activeView.ViewTemplateId != ElementId.InvalidElementId)
            {
                View viewTemplate = doc.GetElement(activeView.ViewTemplateId) as View;
                if (viewTemplate != null)
                {
                    targetView = viewTemplate;
                }
            }
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
                        if (parameterFilter.Id.Value == idsArray[i]) // вместо ElementId.IntegerValue
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
                    targetView.AddFilter(filterElementsList[i].Id);
                    targetView.SetFilterVisibility(filterElementsList[i].Id, true);
                    targetView.SetFilterOverrides(filterElementsList[i].Id, colorOverrideList[i]);
                }

                t.Commit();
            }


                return Result.Succeeded;
            }
            
        }

        

    }
    

