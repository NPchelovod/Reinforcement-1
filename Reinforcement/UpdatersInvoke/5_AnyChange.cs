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

            if (!AllUpdater) { return; }
            //сюда приходят от всех изменений элементы
            try
            {
                //меняем автора элемента
                AutoFillNoteUpdater.AvtorUpdater(data);
            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"Ошибка в AnyChange: {ex.Message}");
            }
        }
        //переподписаться

       
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
