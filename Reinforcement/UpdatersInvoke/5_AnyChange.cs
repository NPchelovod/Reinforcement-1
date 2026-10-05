using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
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
                
                SecretComand(data);
            }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
            }
            finally
            {
                App.InPendingSecret = false;
            }

            try
            {
                
                //LookUsersUpdate(data);
                //не заходим в удаленные элементы!!!
                if (!AllUpdater) { return; }
               // if ( data.GetDeletedElementIds().Count > 0) { return; }
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

       

        public static void SecretComand(UpdaterData data)
        {
            //команда срабатывает внутри группы

            Document doc = data.GetDocument();
            if (doc == null)
            {
                return;
            }
            if (App.InPendingSecret)
            {
                App.InPendingSecret = false;

                
                if (!string.IsNullOrEmpty(App.OnGroupCurrent.Name))
                {
                    Element elemGroup = GetAnyElement(data);//элемент в группе
                    if (elemGroup == null) { return; }
                    ElementId groupId = elemGroup.GroupId;
                    if (groupId == ElementId.InvalidElementId) {  return; }

                    List<Element>  elements = ArmLengthEquels.SelectOrAllElements(false,false);
                    elements = elements.Where(el => el.GroupId == groupId).ToList();
                    //надо найти имеющие туже группу

                    //надо собрать все элементы которые принадлежат данной группе и находятся на виде
                    DeleteDublicateFamily.ReplacedProcess(doc, elements, false, new HashSet<ElementId>(), false);
                }
               
            }
        }

        public static Element GetAnyElement(UpdaterData data)
        {
            Document doc = data.GetDocument();
            if (doc == null)
            {
                return null;
            }
            // Получаем списки элементов
            var addedIds = data.GetAddedElementIds();
            var modifiedIds = data.GetModifiedElementIds();
            if (addedIds.Count > 0)
            {
                Element element = doc.GetElement(addedIds.First());
                return element;
            }
            if (modifiedIds.Count > 0)
            {
                Element element = doc.GetElement(modifiedIds.First());
                return element;
            }
            return null;
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
