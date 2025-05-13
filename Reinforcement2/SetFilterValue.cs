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

namespace SetFilterValue
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class SetFilterValue : IExternalCommand
    {
        static AddInId addinId = new AddInId(new Guid("80E287DF-4399-4DA1-938E-95548C0B6DAF"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (0 == selectedIds.Count) //ничего не выбрано
            {
                TaskDialog.Show("Ошибка!!!", "Ничего не выбрано!");
            }
            else
            {
                string info = "ЗАГОЛОВОК" + "\n\t";
                using (TransactionGroup transactionGroup = new TransactionGroup(doc, "Установить значение фильтра"))
                {
                    transactionGroup.Start();
                    foreach (ElementId selectedId in selectedIds) //просматриваем каждую выбранную спецификацию
                    {
                        Element selectedElements = doc.GetElement(selectedId);
                        ViewSchedule VS = selectedElements as ViewSchedule;
                        var scheduleDefinition = VS.Definition;
                        IList<ScheduleFieldId> scheduleFieldsId = scheduleDefinition.GetFieldOrder(); //получаем Id полей по порядку
                        IList<ScheduleFilter> scheduleFilters = scheduleDefinition.GetFilters();      //получаем фильтры спецификацииы

                        foreach (ScheduleFieldId scheduleFieldId in scheduleFieldsId) //смотрим каждое поле спецификации
                        {
                            var fieldName = scheduleDefinition.GetField(scheduleFieldId).GetName(); //имя поля
                            bool hasMarkaConstr = fieldName.Contains("Марка конструкции"); //проверяем марка конструкции это или нет
                            if (hasMarkaConstr == true)
                            {
                                int scheduleFilterIndex = 0;
                                foreach (ScheduleFilter scheduleFilter in scheduleFilters)
                                {
                                    var fieldId = scheduleFilter.FieldId; //ищем Id поля по которому работает фильтр спецификации
                                    bool test = fieldId == scheduleFieldId; //проверяем совпадает ли фильтр и наше искомое поле
                                    if (test == true)
                                    {
                                        var oldValue = scheduleFilter.GetStringValue();
                                        var newValue = "Пм6";
                                        using (Transaction t = new Transaction(doc, "Изменение значения фильтра"))
                                        {
                                            t.Start();
                                            scheduleFilter.SetValue(newValue);
                                            scheduleDefinition.SetFilter(scheduleFilterIndex, scheduleFilter);
                                            t.Commit();
                                        }
                                    }
                                    scheduleFilterIndex++;
                                }
                            }
                        }
                    }
                    transactionGroup.Assimilate();
                }
                TaskDialog.Show("Норм!", $"{info} Количество изменных спецификаций: {selectedIds.Count.ToString()}");
            }
            return Result.Succeeded;
        }
    }
}