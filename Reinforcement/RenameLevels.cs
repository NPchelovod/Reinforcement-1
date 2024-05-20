using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class RenameLevels : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }
            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "Переименовать уровни"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .ToElements()
                    .OfType<Level>()
                    .ToList();
                    var orderedLevels = from level in levels
                                        orderby level.Elevation ascending
                                        select level;   
                    List<Level> listLevels = orderedLevels.ToList();
                    for (int i = 1; i < levels.Count; i++)
                    {
                        listLevels[i].Name = $"{i}_эт";
                    }
                    for (int i = 1; i < levels.Count; i++)
                    {
                        //string height = UnitUtils.ConvertFromInternalUnits(listLevels[i].Elevation, UnitTypeId.Meters).ToString();
                       string height = listLevels[i].LookupParameter("Фасад").AsValueString() ;
                        int digitCount = height.Count();
                        if (listLevels[i].Elevation > 0)
                        {
                            listLevels[i].Name = $"{i} этаж основной на отм. {height.Insert(digitCount - 3, ",").Insert(0, "+")}";
                        }
                        else
                        {
                            listLevels[i].Name = $"{i} этаж основной на отм. {height.Insert(1, ",").Insert(1, "0")}";
                        }
                    }

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
