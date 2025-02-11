using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reinforcement;
using System.Drawing;
using System.Windows.Forms;

namespace UpdaterChangeWidthAnnotationTag
{
    public class ChangeWidthAnnotationTag : IUpdater
    {
        public static double GetCharacterWidth(Document doc, string text)
        {
            double width = 0;
            Font font = new Font("ISOCPEUR", 3.5f, FontStyle.Regular);
            Size textSize = TextRenderer.MeasureText(text, font);
            width = textSize.Width * 0.8;
            return width;

        }
        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            var ids = data.GetModifiedElementIds().ToList();

            foreach (var id in ids)
            {
                var element = doc.GetElement(id);
                string firstText = element.LookupParameter("Текст верх").AsString();
                string secondText = element.LookupParameter("Текст низ").AsString();
                var text = firstText.Count() > secondText.Count() ? firstText : secondText;
                double width = GetCharacterWidth(doc, text);
                width = RevitAPI.ToFoot(width);

                element.LookupParameter("Ширина полки").Set(width);
                
                //TaskDialog.Show("Revit updater",$"Имя измененного элемента {element.Name}");
            }
        }

        public string GetAdditionalInformation()
        {
            return string.Empty;
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.Annotations;
        }

        public UpdaterId GetUpdaterId()
        {
            return new UpdaterId(RevitAPI.UiApplication.ActiveAddInId,
                new Guid("05EA6041-8ED1-4A7D-AF1B-660AB714678A"));
        }

        public string GetUpdaterName()
        {
            return "Updater для длины линии выноски";
        }
    }
}
