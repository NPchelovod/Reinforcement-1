using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Windows.Controls;

namespace Reinforcement
{
    public class CreateSchedulesWall
    {
        public CreateSchedulesWall(Document document)
        {

        }
        public string ConstructionMark { get; set; }
        public string ViewDestination { get; set; }

        public void CopyScheduleView(string scheduleViewName, string filterName, IList<View> viewsList, string constructionMark, string viewDestination)
        {
            foreach (var view in viewsList)
            {
                View dependentView = null;
                ElementId newViewId = null;
                Parameter parameter = null;
                if (view.Name == scheduleViewName)
                {
                    newViewId = view.Duplicate(ViewDuplicateOption.Duplicate);
                    dependentView = view.Document.GetElement(newViewId) as View;
                    string viewDestinationIndex = viewDestination.Split('_')[0];
                    string dependentViewName = dependentView.Name;
                    string newViewName = dependentViewName.Replace("00", viewDestinationIndex).Replace(" копия 1", "").Replace("Пм1", constructionMark);
                    dependentView.Name = newViewName;
                    parameter = dependentView.LookupParameter("ADSK_Назначение вида");
                    parameter.Set(ViewDestination);
                    ViewSchedule viewSchedule = dependentView as ViewSchedule;
                    var scheduleDefinition = viewSchedule.Definition;
                    IList<ScheduleFieldId> scheduleFieldsId = scheduleDefinition.GetFieldOrder(); //получаем Id полей по порядку
                    IList<ScheduleFilter> scheduleFilters = scheduleDefinition.GetFilters();      //получаем фильтры спецификации

                    foreach (ScheduleFieldId scheduleFieldId in scheduleFieldsId) //смотрим каждое поле спецификации
                    {
                        var fieldName = scheduleDefinition.GetField(scheduleFieldId).GetName(); //имя поля
                        bool hasMarkaConstr = fieldName.Contains(filterName); //проверяем марка конструкции это или нет
                        if (hasMarkaConstr == true)
                        {
                            int scheduleFilterIndex = 0;
                            foreach (ScheduleFilter scheduleFilter in scheduleFilters)
                            {
                                var fieldId = scheduleFilter.FieldId; //ищем Id поля по которому работает фильтр спецификации
                                bool test = fieldId == scheduleFieldId; //проверяем совпадает ли фильтр и наше искомое поле
                                if (test == true)
                                {
                                    scheduleFilter.SetValue(constructionMark);
                                    scheduleDefinition.SetFilter(scheduleFilterIndex, scheduleFilter);
                                }
                                scheduleFilterIndex++;
                            }
                        }
                    }
                    break;
                }
            }


        }

        public void SchedulesDuplication()
        {
            var col = new FilteredElementCollector(RevitAPI.Document).OfClass(typeof(View)).Cast<View>().ToList();

            List<string> scheduleNamesList = new List<string>();
            scheduleNamesList.Add("21_Ядж1-01_Арматура_3. Фоновая");
            scheduleNamesList.Add("21_Ядж1-01_Арматура_4. Гнутые");
            scheduleNamesList.Add("21_Ядж1-01_Арматура_5. Прямые");
            scheduleNamesList.Add("21_Ядж1-01_ВРС");
            scheduleNamesList.Add("21_Ядж1-01_Ведомость деталей");
            

            using (Transaction t = new Transaction(RevitAPI.Document, "Copy view"))
            {
                t.Start();
                foreach (var scheduleName in scheduleNamesList)
                {
                    CopyScheduleView(scheduleName, "Марка конструкции", col, ConstructionMark, ViewDestination);
                }
                CopyScheduleView("21_Ядж1-01_Бетон", "Марка", col, ConstructionMark, ViewDestination);
                t.Commit();
            }

        }
    }
}

