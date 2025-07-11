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
//using System.Windows;

namespace Updaters
{
    public class ChangeWidthAnnotationTag : IUpdater
    {



        public static double GetCharacterWidth(Document doc, string text, Single H_size = 3.5f)
        {
            double width = 0;
            Font font = new Font("ISOCPEUR", H_size, FontStyle.Regular);


            Size textSize = TextRenderer.MeasureText(text, font);
            int len_text = textSize.Width;

            double k_sg = 75;// 0.0045 * len_text + 0.6248;

            if (H_size == 3.5f && len_text > 2)
            {
                //k_sg = 0.0045* len_text + 0.6248;// 0.7863 + 0.0029 * text.Count();//0.8*192/182*(114+8)/130;
                if (len_text>30) // для большей длины не корректно
                { len_text = 28; }
                k_sg = - 0.00005 * len_text * len_text + 0.0062 * len_text + 0.6176;
            }
            if (H_size == 2.5f)
            {
                if (len_text > 30) // для большей длины не корректно
                { len_text = 28; }
                k_sg = 0.5524+0.0025* len_text;
            }


            width = textSize.Width * k_sg;
            return width;

        }
        static bool IsFontInstalled(string fontName)
        {
            using (var fontsCollection = new System.Drawing.Text.InstalledFontCollection())
            {
                return fontsCollection.Families.Any(f => f.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase));
            }
        }
        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();

            var ids = data.GetModifiedElementIds().ToList();


            foreach (var id in ids)
            {
                var element = doc.GetElement(id);
                //string Name = element.Name;

                try
                {
                    string name = element.LookupParameter("Семейство и типоразмер").AsValueString();
                    if (name.Contains("Выноска") || name.Contains("выноска"))
                    {
                        string firstText = element.LookupParameter("Текст верх").AsString();
                        string secondText = element.LookupParameter("Текст низ").AsString();
                        var text = firstText.Count() > secondText.Count() ? firstText : secondText;
                        if (!IsFontInstalled("ISOCPEUR"))
                        {
                            TransparentNotificationWindow.ShowNotification("Не удалось найти шрифт ISOCPEUR\nАвтоудлинение выноски не сработало", RevitAPI.UiDocument, 3);
                            return;
                        }

                        //var elementType = element as ElementType;


                        Single H_size = 3.5f;
                        if (name.Contains("2.5") || name.Contains("2,5"))
                        {
                            H_size = 2.5f;
                        }

                        double width = GetCharacterWidth(doc, text, H_size);

                        width = RevitAPI.ToFoot(width);

                        element.LookupParameter("Ширина полки").Set(width);

                        //TaskDialog.Show("Revit updater",$"Имя измененного элемента {element.Name}");
                    }
                }
                catch (Exception) { continue; }

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
            //Мы должны вернуть UpdaterId, который состоит из 2 частей: AddinId, который должен совпасть с AddinId нашего приложения, и Guid апдейтера. Я решил использовать свойство ActiveAddinId класса Application, почему бы и нет:
            return new UpdaterId(RegisterUpdater.addInId,
                new Guid("05EA6041-8ED1-4A7D-AF1B-660AB714678A"));
        }

        public string GetUpdaterName()
        {
            return "Updater для длины линии выноски";
        }
    }
}
