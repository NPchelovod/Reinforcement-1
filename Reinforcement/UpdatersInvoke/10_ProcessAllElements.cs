using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
namespace Reinforcement
{
    public enum ETypeChangeEl
    {
        None = 0,
        AddedElements,
        ModifiedElements,
        GeomModifiedElements,
        DeleteModifiedElements,

    }
    public static class ProcessAllElements
    {
        
        public static void ProcessSnapshot(SnapshotModificationElements snapshotModificationElements)
        {
            //и все элементы мы обрабатываем
            foreach (Element el in snapshotModificationElements.AddedElements)
            {
                ProcessElement(el, ETypeChangeEl.AddedElements);
            }
            foreach (Element el in snapshotModificationElements.ModifiedElements)
            {
                //тут нет элементов из AddedElements
                ProcessElement(el, ETypeChangeEl.ModifiedElements);
            }
            foreach (Element el in snapshotModificationElements.GeomModifiedElements)
            {
                //тут могут быть элементы из AddedElements и или ModifiedElements
                ProcessElement(el, ETypeChangeEl.ModifiedElements);
            }

        }

        public static void ProcessElement(Element el, ETypeChangeEl eTypeChangeEl)
        {
            if (el == null) { return; }
            BuiltInCategory bic = el.Category.BuiltInCategory;

            switch (bic)
            {
                case (BuiltInCategory.OST_GenericAnnotation):
                    break;
                case (BuiltInCategory.OST_GenericModel):
                    break;
                default: 
                    break;
            }

        }




    }
}
