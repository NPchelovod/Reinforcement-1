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
using System.Windows.Controls;
using System.Diagnostics;
using System.Threading;

namespace Reinforcement
{
    public partial class PeriodicHandler
    {
        private static void AnnotationWriter(DateTime dateTime, Document doc, Element element, ETypeChangeEl eTypeChangeEl)
        {
            var param = element.LookupParameter("Семейство и типоразмер");
            if(param == null) { return; }
            string name= param.AsValueString().ToLower();
            if (!name.Contains("выноска")) { return; }
            var paramTop = element.LookupParameter("Текст верх");
            if (paramTop == null) { return; }

            lookWidth(doc, element, name, paramTop);

        }
        private readonly static List<string> nameWidths = new List<string> { "ЕС_Ширина полки", "Ширина полки" };
        private static bool lookWidth(Document doc, Element element, string name, Parameter paramTop)
        {
            try
            {

                string firstText = paramTop.AsString();
                string secondText = element.LookupParameter("Текст низ")?.AsString();

                if (!IsFontInstalled("ISOCPEUR"))
                {
                    TransparentNotificationWindow.ShowNotification("Не удалось найти шрифт ISOCPEUR\nАвтоудлинение выноски не сработало", RevitAPI.UiDocument, 3);
                    return false;
                }

                //var elementType = element as ElementType;


                Single H_size = 3.5f;
                if (name.Contains("2.5") || name.Contains("2,5"))
                {
                    H_size = 2.5f;
                }

                double width1 = RevitAPI.ToFoot(GetCharacterWidth(doc, firstText, H_size));
                double width2 = RevitAPI.ToFoot(GetCharacterWidth(doc, secondText, H_size));
                double width = Math.Max(width1, width2); //может быть нулевым, ну вроде не опасно


                foreach (var wid in nameWidths)// возможные имена
                {
                    Parameter paramW = element.LookupParameter(wid);
                    if (paramW != null)
                    {
                        SetIfChanged(paramW, width);
                        //paramW.Set(width);
                        break;
                    }
                }

                Parameter param = element.LookupParameter("ЕС_Ширина текста верх");
                if (param != null && !param.IsReadOnly)
                {
                    //param.Set(width1);
                    SetIfChanged(param, width1);
                    param = element.LookupParameter("ЕС_Ширина текста низ");
                    if (param != null && !param.IsReadOnly)
                    {
                        SetIfChanged(param, width2);
                        //param.Set(width2); 
                    
                    }
                }

                

            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
                return false;
            }
            return true;
        }
        static bool IsFontInstalled(string fontName)
        {
            using (var fontsCollection = new System.Drawing.Text.InstalledFontCollection())
            {
                return fontsCollection.Families.Any(f => f.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase));
            }
        }

        static double GetCharacterWidth(Document doc, string text, Single H_size = 3.5f)
        {
            //вычисление ширины текста
            double width = 0;
            if (string.IsNullOrEmpty(text))
            { return width; }

            Font font = new Font("ISOCPEUR", H_size, FontStyle.Regular);


            Size textSize = TextRenderer.MeasureText(text, font);
            int len_text = textSize.Width;

            double k_sg = 75;// 0.0045 * len_text + 0.6248;

            if (H_size == 3.5f && len_text > 2)
            {
                //k_sg = 0.0045* len_text + 0.6248;// 0.7863 + 0.0029 * text.Count();//0.8*192/182*(114+8)/130;
                if (len_text > 30) // для большей длины не корректно
                { len_text = 28; }
                k_sg = -0.00005 * len_text * len_text + 0.0062 * len_text + 0.6176;
            }
            else if (H_size == 2.5f)
            {
                if (len_text > 30) // для большей длины не корректно
                { len_text = 28; }
                k_sg = 0.5524 + 0.0025 * len_text;
            }


            width = textSize.Width * k_sg;
            return width;

        }
    }
}