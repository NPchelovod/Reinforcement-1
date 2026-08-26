using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

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

            //меняем автора элемента
            AutoFillNoteUpdater.AvtorUpdater(data);
        }
    }
}
