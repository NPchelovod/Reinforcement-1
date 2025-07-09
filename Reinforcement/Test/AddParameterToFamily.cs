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

namespace Reinforcement.Test
{
    [Transaction(TransactionMode.Manual)]

    public class AddParameterToFamily : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Проверяем, открыто ли семейство
            if (!doc.IsFamilyDocument)
            {
                TaskDialog.Show("Ошибка", "Этот код работает только в семействах.");
                return Result.Failed;
            }

            FamilyManager famManager = doc.FamilyManager;


            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    Category entoyrageCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Entourage);

                    // Проверяем, нет ли уже такого параметра
                    foreach (FamilyParameter parameter in famManager.Parameters)
                    {
                        if (parameter.Definition.Name == "Тип0")
                        {
                            TaskDialog.Show("Информация", "Параметр уже существует.");
                            return Result.Cancelled;
                        }
                    }

                    FamilyParameter param;
                    // Добавляем новый параметр

                    famManager.AddParameter("Обозначение План", GroupTypeId.Graphics, entoyrageCategory, true);
                    famManager.AddParameter("Обозначение Разрез", GroupTypeId.Graphics, entoyrageCategory, true);
                    famManager.AddParameter("Тип0", GroupTypeId.AnalysisResults, entoyrageCategory, true);
                    famManager.AddParameter("Тип1", GroupTypeId.AnalysisResults, entoyrageCategory, true);
                    famManager.AddParameter("Тип2", GroupTypeId.AnalysisResults, entoyrageCategory, true);
                    famManager.AddParameter("Тип3", GroupTypeId.AnalysisResults, entoyrageCategory, true);
                    famManager.AddParameter("Тип4", GroupTypeId.AnalysisResults, entoyrageCategory, true);
                    famManager.AddParameter("Тип5", GroupTypeId.AnalysisResults, entoyrageCategory, true);
                    famManager.AddParameter("Тип6", GroupTypeId.AnalysisResults, entoyrageCategory, true);
                    famManager.AddParameter("Тип7", GroupTypeId.AnalysisResults , entoyrageCategory, true);
                    famManager.AddParameter("Тип8", GroupTypeId.AnalysisResults, entoyrageCategory, true);
         
                    t.Commit();
                    t.Start();

                    param = famManager.AddParameter("Обозначение_План_Тип", GroupTypeId.AnalysisResults, SpecTypeId.Int.Integer, true);
                    famManager.SetFormula(param, "if(Обозначение План = Тип0, 0, if(Обозначение План = Тип1, 1, if(Обозначение План = Тип2, 2, if(Обозначение План = Тип3, 3, if(Обозначение План = Тип4, 4, if(Обозначение План = Тип5, 5, if(Обозначение План = Тип6, 6, if(Обозначение План = Тип7, 7, 8))))))))");

                    param = famManager.AddParameter("Обозначение_Разрез_Тип", GroupTypeId.AnalysisResults, SpecTypeId.Int.Integer, true);
                    famManager.SetFormula(param, "if(Обозначение Разрез = Тип0, 0, if(Обозначение Разрез = Тип1, 1, if(Обозначение Разрез = Тип2, 2, if(Обозначение Разрез = Тип3, 3, if(Обозначение Разрез = Тип4, 4, if(Обозначение Разрез = Тип5, 5, if(Обозначение Разрез = Тип6, 6, if(Обозначение Разрез = Тип7, 7, 8))))))))");



                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                //Код в случае ошибки
                MessageBox.Show("Чет пошло не так!\n" + ex.Message);
                return Result.Failed;
            }

            TaskDialog.Show("Успех", "Параметр успешно добавлен!");

            return Result.Succeeded;
        }
    }
}
