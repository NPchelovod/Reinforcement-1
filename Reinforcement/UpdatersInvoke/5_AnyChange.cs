using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Updaters;

namespace Reinforcement
{
    public static class AnyChange
    {
        //тут запиши какие хочешь действия на любые действия

        public static bool AllUpdater = true;
        public static void Execute(UpdaterData data)
        {

            
            //сюда приходят от всех изменений элементы
            try
            {
                LookUsersUpdate(data);
                //не заходим в удаленные элементы!!!
                if (!AllUpdater || data.GetDeletedElementIds().Count > 0) { return; }
                //меняем автора элемента
                AutoFillNoteUpdater.AvtorUpdater(data);
            }
            catch (Exception ex) 
            {
               App_Apdater_1.AppErrors.LogError(ex);
                Debug.WriteLine($"Ошибка в AnyChange: {ex.Message}");
            }
        }
        //переподписаться

       public static void LookUsersUpdate(UpdaterData data)
        {
            //var addedIds = data.GetAddedElementIds();
            
            //var deletes = data.GetDeletedElementIds();
            if (data.GetAddedElementIds().Count > 0)
            {
                App_Apdater_1.LookUsers.Update("AnyChange.Execute.addedIds", EDocStatsOptions.Invoker); //"AnyChange.Execute");
                return;
            }
            else if(data.GetDeletedElementIds().Count>0)
            {
                App_Apdater_1.LookUsers.Update("AnyChange.Execute.deletes", EDocStatsOptions.Invoker); //"AnyChange.Execute");
                return;
            }

            var modifiedIds = data.GetModifiedElementIds();
            
            if (modifiedIds.Count > 0)
            {

                ElementId id = modifiedIds.First();
                var doc = data.GetDocument();
                // --- 1. Изменение ГЕОМЕТРИИ (перемещение, изменение формы) ---
                if (data.IsChangeTriggered(id, Element.GetChangeTypeGeometry()))
                {
                    App_Apdater_1.LookUsers.Update("AnyChange.Execute.modifiedIds.geometry", EDocStatsOptions.Invoker); //"AnyChange.Execute");
                }
                // --- 3. Изменение встроенного параметра (пример: Комментарии) ---
                //else if (data.IsChangeTriggered(id, Element.GetChangeTypeParameter(id)))
                //{
                //    App_Apdater_1.LookUsers.Update("AnyChange.Execute.modifiedIds.geometry", EDocStatsOptions.Invoker); //"AnyChange.Execute");
                //}
                else
                {
                    App_Apdater_1.LookUsers.Update("AnyChange.Execute.modifiedIds", EDocStatsOptions.Invoker); //"AnyChange.Execute");
                }
            }
            else
            {
                App_Apdater_1.LookUsers.Update("AnyChange.Execute", EDocStatsOptions.Invoker); //"AnyChange.Execute");
            }
        }

        public static void PodpiskaAll()
        {
            var app = App.Application;//RevitAPI.UiApplication;
            if (app != null)
            {
                RegisterUpdater.addInId = app.ActiveAddInId;
                RegisterUpdater.Register();

                RegisterZakladkaUpdater.addInId = app.ActiveAddInId;
                RegisterZakladkaUpdater.Register();

                RegisterAutoFillUpdater.addInId = app.ActiveAddInId;

                RegisterAutoFillUpdater.Register();
            }
        }
    }
}
