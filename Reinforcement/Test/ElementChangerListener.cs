using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class ElementChangerListener : IExternalCommand
    {
        public void Register(UIApplication uiapp)
        {
            uiapp.FormulaEditing += OnFormulaEditing;
        }
        public void Unregister(UIApplication uiapp)
        {
            uiapp.FormulaEditing -= OnFormulaEditing;
        }
        private void OnFormulaEditing(object sender, FormulaEditingEventArgs e)
        {
            string param = e.Formula;
            MessageBox.Show($"{param}");
        }
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
                Register(uiapp);

                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
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
    [Transaction(TransactionMode.Manual)]

    public class UnsubscribeFormulaEditingListener : IExternalCommand
    {
        // Основной метод Execute для отписки от события
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            // Отписка от события FormulaEditing
            if (uiapp.Application != null)
            {
                uiapp.FormulaEditing -= OnFormulaEditing;
            }

            MessageBox.Show("Отписка от события FormulaEditing выполнена.");

            using (Transaction t = new Transaction(doc, "действие"))
            {
                t.Start();
                //Тут пишем основной код для изменения элементов модели
                t.Commit();
            }
            return Result.Succeeded;
        }

        // Обработчик события FormulaEditing для вывода в MessageBox
        private void OnFormulaEditing(object sender, FormulaEditingEventArgs e)
        {
            string param = e.Formula; // Получаем параметр, который редактируется
            if (param != null)
            {
                // Показываем информацию о параметре
                MessageBox.Show($"Редактируется параметр: {param}");
            }
        }
    }
}
