using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExcelDataReader;
using System.Windows.Controls;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CreateToposolidFromExcel : IExternalCommand
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
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Title = "Выберите Excel-файл",
                    Filter = "Excel Files|*.xls;*.xlsx",
                    Multiselect = false
                };
                string filePath;
                if (openFileDialog.ShowDialog() == DialogResult.OK) // Если пользователь выбрал файл
                {
                    filePath = openFileDialog.FileName;
                    MessageBox.Show("Выбран файл: " + filePath);
                }
                else
                {
                    MessageBox.Show("Файл не был выбран.");
                    return Result.Cancelled;
                }

                FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(ElementType))
                .OfCategory(BuiltInCategory.OST_Toposolid);

                ElementType existingType = collector.First() as ElementType;

                if (existingType == null)
                {
                    MessageBox.Show("Не найдено ни одного базового типоразмера категории топотела!");
                    return Result.Failed;
                }

                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    List<List<object>> columns = new List<List<object>>();

                    reader.Read();//skip first row
                    int columnCount = reader.FieldCount;
                    for (int col = 1; col < columnCount; col++)
                    {
                        columns.Add(new List<object>());
                    }

                    while (reader.Read())
                    {
                        for (int col = 1; col < columnCount; col++)
                        {
                            var value = reader.GetValue(col);
                            columns[col - 1].Add(value);
                        }
                    }

                    CreateToposolid(doc, existingType, columns);
                    
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


        private void CreateToposolid(Document doc, ElementType existingType, List<List<object>> columns)
        {
            using (Transaction t = new Transaction(doc, "действие"))
            {
                t.Start();
                //Тут пишем основной код для изменения элементов модели
                List<XYZ> points = new List<XYZ>();

                foreach (var column in columns)
                {
                    double X = double.Parse(column[0].ToString().Replace(".",","));
                    double Y = double.Parse(column[1].ToString().Replace(".",","));
                    var height = column[3].ToString().Split('+')[1].Trim();
                    double Z = double.Parse(column[2].ToString().Replace(".",",")) - double.Parse(height.Replace(".",","));
                    
                    X = RevitAPI.ToFoot(X * 1000);
                    Y = RevitAPI.ToFoot(Y * 1000);
                    Z = RevitAPI.ToFoot(Z * 1000);

                    points.Add(new XYZ(X, Y, Z));
                }



                //Дублируем типоразмер              
                //ElementType newType = existingType.Duplicate("ИГЭ №1");

                Toposolid.Create(doc, points, existingType.Id, new ElementId((long)1751));

                t.Commit();
            }

        }
    }
}
