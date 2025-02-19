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

    public class ChangeReinfFamily : IExternalCommand
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

            FamilySymbol newElement = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault(f => f.Family.Name == "ЕС_А-01_Распределение по прямой_П-равнопол_Г" && f.Name == "А500C");


            Selection sel = uidoc.Selection;
            List<ElementId> ids = sel.GetElementIds().ToList();


            try //ловим ошибку
            {
                using (TransactionGroup tg = new TransactionGroup(doc, "действие"))
                {
                    tg.Start();
                    using (Transaction t = new Transaction(doc, "действие"))
                    {
                        t.Start();
                        //Тут пишем основной код для изменения элементов модели
                        foreach (ElementId id in ids)
                        {
                            Element element = doc.GetElement(id);
                            FamilyInstance familyInstance = element as FamilyInstance;
                            LocationCurve location = element.Location as LocationCurve;
                            Line position = location.Curve as Line;

                            ParameterSet parameters = element.Parameters;
                            bool flipFace = familyInstance.FacingFlipped;
                            bool flipHand = familyInstance.HandFlipped;



                            if (!newElement.IsActive) newElement.Activate();

                            FamilyInstance newInstance = doc.Create.NewFamilyInstance(position, newElement, doc.ActiveView);

                            foreach (Parameter parameter in parameters)
                            {
                                if (parameter.IsReadOnly || !((InternalDefinition)parameter.Definition).Visible || !parameter.HasValue)
                                    continue;
                                // Ищем такой же параметр в новом экземпляре
                                Parameter newParam = newInstance.LookupParameter(parameter.Definition.Name);
                                if (newParam != null && !newParam.IsReadOnly)
                                {
                                    // Копируем значение
                                    switch (parameter.StorageType)
                                    {
                                        case StorageType.String:
                                            newParam.Set(parameter.AsString());
                                            break;
                                        case StorageType.Double:
                                            newParam.Set(parameter.AsDouble());
                                            break;
                                        case StorageType.Integer:
                                            newParam.Set(parameter.AsInteger());
                                            break;
                                    }
                                }
                            }
                            t.Commit();
                            t.Start();
                            parameters = newInstance.Parameters;
                            foreach (Parameter parameter in parameters)
                            {
                                Parameter newParam = familyInstance.LookupParameter(parameter.Definition.Name);
                                if (parameter.Definition.Name == "Семейство")
                                {
                                    newParam.Set(newElement.Id);
                                    break;
                                }
                            }
                            foreach (Parameter parameter in parameters)
                            {
                                if (parameter.IsReadOnly || !((InternalDefinition)parameter.Definition).Visible || !parameter.HasValue)
                                    continue;
                                // Ищем такой же параметр в новом экземпляре
                                Parameter newParam = familyInstance.LookupParameter(parameter.Definition.Name);
                                if (newParam != null && !newParam.IsReadOnly)
                                {
                                    // Копируем значение
                                    switch (parameter.StorageType)
                                    {
                                        case StorageType.String:
                                            newParam.Set(parameter.AsString());
                                            break;
                                        case StorageType.Double:
                                            newParam.Set(parameter.AsDouble());
                                            break;
                                        case StorageType.Integer:
                                            newParam.Set(parameter.AsInteger());
                                            break;
                                    }
                                }
                            }
                            doc.Delete(newInstance.Id);
                        }                     
                        t.Commit();
                    }
                    tg.Assimilate();
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
