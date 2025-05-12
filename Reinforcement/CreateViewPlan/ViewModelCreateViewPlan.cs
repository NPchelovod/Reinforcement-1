using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Reinforcement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Reinforcement
{
    public class ViewModelCreateViewPlan
    {
        public ViewModelCreateViewPlan(List<Level> levels)
        {
            Levels = levels;
        }
        public List<Level> Levels { get; set; } = new List<Level>();

        public string MarkElevation { get; set; }

        public string Prefix { get; set; }

        public Level SelectedLevel { get; set; }
        public void CreateViewPlan()
        {
            Document doc = RevitAPI.Document;
            using (Transaction t = new Transaction(doc, "EC: Создание вида"))
            {
                t.Start();
                var viewTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .ToElements()
                    .OfType<ViewFamilyType>()
                    .ToList();

                //Get Ids of parameters
                BuiltInParameter viewNameId = BuiltInParameter.VIEW_NAME;
                BuiltInParameter viewportId = BuiltInParameter.VIEW_DESCRIPTION;

                //Create the additional text
                List<string> armText = new List<string>() { 
                    "Схема расположения основной арматуры и каркасов в плите", "Схема расположения дополнительной нижней арматуры и обрамляющих стержней в плите",
                    "Схема расположения дополнительной верхней арматуры в плите",
                    "Основная и каркасы", "Нижняя и обрамление", "Дополнительная верхняя"
                };

                foreach (ViewFamilyType viewType in viewTypes)
                {
                    if (viewType.Name == "План несущих конструкций")
                    {
                        var viewTypeStructural = viewType.Id;
                        var viewsList = new List<ViewPlan>();
                       
                        //Create views and set parameters
                        for (int i = 0; i < 3; i++ )
                        {
                            viewsList.Add(ViewPlan.Create(doc, viewTypeStructural, SelectedLevel.Id));
                            Parameter viewName = viewsList[i].get_Parameter(viewNameId);
                            Parameter viewportName = viewsList[i].get_Parameter(viewportId);
                            viewName.Set($"{Prefix}_{MarkElevation.Substring(0, 3)}_{armText[i + 3]}");
                            viewportName.Set($"{armText[i]} {MarkElevation}");
                            Parameter directory = viewsList[i].LookupParameter("◦ Директория");
                            directory.Set($"{Prefix}_РД");
                            Parameter chapter = viewsList[i].LookupParameter("◦ Раздел");
                            chapter.Set("АРМ_Пм");
                        }                                                                      
                        
                        //Create the sheet and rename
                        ElementId titleBlockTypeId = new ElementId((long)218938);
                        var newSheet = ViewSheet.Create(doc, titleBlockTypeId);
                        BuiltInParameter paramId = BuiltInParameter.SHEET_NAME;
                        Parameter sheetName = newSheet.get_Parameter(paramId);
                        sheetName.Set($"Схемы армирования плиты монолитной {MarkElevation}");
                        
                        //Place views on the sheet
                        for (int  i = 0; i < 3; i++)
                        {
                            Viewport.Create(doc, newSheet.Id, viewsList[i].Id, new XYZ(i*3, 0, 0));
                        }
                    }
                }                
                t.Commit();
            }

        }
    }
}