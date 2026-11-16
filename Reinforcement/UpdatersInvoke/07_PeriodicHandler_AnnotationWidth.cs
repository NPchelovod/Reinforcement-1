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
        static readonly List<string> _familyVinoska = new List<string>() { "выноска",  };
        static readonly BuiltInCategory _builtInCategoryvinoska = BuiltInCategory.OST_GenericAnnotation;
        private static void AnnotationWriter(DateTime dateTime, BuiltInCategory bic, Document doc, Element element, ETypeChangeEl eTypeChangeEl)
        {

            if (bic != _builtInCategoryvinoska) { return; }

            var param = element.LookupParameter("Семейство и типоразмер");
            if(param == null) { return; }
            string name = param.AsValueString();                 // может быть null
            if (string.IsNullOrEmpty(name)) return;
            name = name.ToLowerInvariant();
            if (!name.Contains("выноска")) return;

            var paramTop = element.LookupParameter("Текст верх");
            if (paramTop == null) return;
            // --- НОВАЯ ЛОГИКА: Проверяем наличие стрелки ---
            bool hasArrow = HasLeaderArrowhead(element);
            // ---------------------------------------------
            lookWidth(doc, element, name, paramTop, hasArrow);

        }
        private readonly static List<string> nameWidths = new List<string> { "ЕС_Ширина полки", "Ширина полки" };
        private static int _fontWarningShown;   // 0 = не показывали
        private static bool lookWidth(Document doc, Element element, string name, Parameter paramTop, bool hasArrow)
        {
            try
            {

                string firstText = paramTop.AsString();
                string secondText = element.LookupParameter("Текст низ")?.AsString();

                if (!IsFontInstalled("ISOCPEUR"))
                {
                    if (Interlocked.Exchange(ref _fontWarningShown, 1) == 0)
                    {
                        TransparentNotificationWindow.ShowNotification(
                            "Не удалось найти шрифт ISOCPEUR\nАвтоудлинение выноски не сработало",
                            RevitAPI.UiDocument, 3);
                    }
                    return false;
                }

                //var elementType = element as ElementType;


                Single H_size = 3.5f;
                if (name.Contains("2.5") || name.Contains("2,5"))
                {
                    H_size = 2.5f;
                }

                //double width1 = RevitAPI.ToFoot(GetCharacterWidth(doc, firstText, H_size));
                //double width2 = RevitAPI.ToFoot(GetCharacterWidth(doc, secondText, H_size));

                double width1 = RevitAPI.ToFoot(GetCharacterWidthPrecise(firstText, H_size, hasArrow));
                double width2 = RevitAPI.ToFoot(GetCharacterWidthPrecise(secondText, H_size, hasArrow));
                double width = Math.Max(width1, width2); //может быть нулевым, ну вроде не опасно
                if(width <=0)
                { 
                    width = RevitAPI.ToFoot(1); 
                }

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
                AppErrors.LogError(ex);
                return false;
            }
            return true;
        }
        /// <summary>
        /// Проверяет, назначена ли выноске стрелка (arrowhead).
        /// </summary>
        private static bool HasLeaderArrowhead(Element element)
        {
            // 1. Проверяем параметр LEADER_ARROWHEAD у экземпляра
            Parameter arrowParam = element.get_Parameter(BuiltInParameter.LEADER_ARROWHEAD);
            if (arrowParam != null && arrowParam.StorageType == StorageType.ElementId)
            {
                ElementId arrowId = arrowParam.AsElementId();
                if (arrowId != null && arrowId != ElementId.InvalidElementId)
                    return true;
            }

            // 2. Если у экземпляра параметра нет (или он пуст), проверяем его Тип
            //    (в семействах Generic Annotation стрелка часто задается на уровне типа)
            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                ElementType elemType = element.Document.GetElement(typeId) as ElementType;
                if (elemType != null)
                {
                    Parameter typeArrowParam = elemType.get_Parameter(BuiltInParameter.LEADER_ARROWHEAD);
                    if (typeArrowParam != null && typeArrowParam.StorageType == StorageType.ElementId)
                    {
                        ElementId arrowId = typeArrowParam.AsElementId();
                        if (arrowId != null && arrowId != ElementId.InvalidElementId)
                            return true;
                    }
                }
            }

            return false;
        }
        // ============ Кэш шрифтов ============
        private static readonly Dictionary<float, Font> _fontCache = new Dictionary<float, Font>();
        private static Font GetCachedFont(float heightMm)
        {
            lock (_fontCache)
            {
                if (!_fontCache.TryGetValue(heightMm, out var f) || f == null)
                {
                    f = new Font("ISOCPEUR", heightMm, FontStyle.Regular, GraphicsUnit.Millimeter);
                    _fontCache[heightMm] = f;
                }
                return f;
            }
        }

        // ============ Кэш "шрифт установлен" ============
        private static readonly Dictionary<string, bool> _fontInstalledCache =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        static bool IsFontInstalled(string fontName)
        {
            lock (_fontInstalledCache)
            {
                if (_fontInstalledCache.TryGetValue(fontName, out var cached))
                    return cached;

                bool installed;
                using (var fontsCollection = new System.Drawing.Text.InstalledFontCollection())
                {
                    installed = fontsCollection.Families.Any(f =>
                        f.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase));
                }
                _fontInstalledCache[fontName] = installed;
                return installed;
            }
        }

        // ============ Статический контекст измерения ============
        // Graphics не thread-safe → [ThreadStatic]. Пул-потоки живут долго,
        // утечки нет: 1x1 Bitmap = ~4 байта + 1 хэндл GDI.
        [ThreadStatic] private static Bitmap _measureBmp;
        [ThreadStatic] private static Graphics _measureG;
        private static readonly StringFormat _measureFormat = CreateTypographicFormat();

        private static StringFormat CreateTypographicFormat()
        {
            // Клон ОБЯЗАТЕЛЕН: GenericTypographic — глобальный shared instance,
            // мутировать его флаги нельзя.
            var f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            return f;
        }
        // Эмпирическая поправка между MeasureString (GDI+) и рендерингом Revit
        // для шрифта ISOCPEUR. Откалибрована сравнением с TextNote.Width.
        private const double IsoCpeurWidthCorrection = 1.092557;
        private static double GetCharacterWidthPrecise(string text, float heightMm, bool hasArrow)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            try
            {
                if (_measureG == null)
                {
                    _measureBmp = new Bitmap(1, 1);
                    _measureG = Graphics.FromImage(_measureBmp);
                    _measureG.PageUnit = GraphicsUnit.Millimeter;
                }

                var font = GetCachedFont(heightMm);

                // PointF-overload — без ограничения ширины и без переносов.
                SizeF size = _measureG.MeasureString(text, font, new PointF(0, 0), _measureFormat);
                double add = AddHvostic(heightMm);
                return add+size.Width * IsoCpeurWidthCorrection;   // ваша эмпирическая поправка, мм
            }
            catch
            {
                return 0;
            }
        }
        private static double AddHvostic(float heightMm)
        {
            //добавка чтобы полки торчали по бокам
            if(Math.Abs(heightMm -3.5)<Math.Abs(heightMm - 2.5))
            {
                //значит шрифт 2.5
                return 1.5;
            }
            else
            {
                return 2.5;
            }
        }

        //static double GetCharacterWidth(Document doc, string text, Single H_size = 3.5f)
        //{
        //    //вычисление ширины текста
        //    double width = 0;
        //    if (string.IsNullOrEmpty(text))
        //    { return width; }

        //    Font font = new Font("ISOCPEUR", H_size, FontStyle.Regular);


        //    Size textSize = TextRenderer.MeasureText(text, font);
        //    int len_text = textSize.Width;

        //    double k_sg = 75;// 0.0045 * len_text + 0.6248;

        //    if (H_size == 3.5f && len_text > 2)
        //    {
        //        //k_sg = 0.0045* len_text + 0.6248;// 0.7863 + 0.0029 * text.Count();//0.8*192/182*(114+8)/130;
        //        if (len_text > 30) // для большей длины не корректно
        //        { len_text = 28; }
        //        k_sg = -0.00005 * len_text * len_text + 0.0062 * len_text + 0.6176;
        //    }
        //    else if (H_size == 2.5f)
        //    {
        //        if (len_text > 30) // для большей длины не корректно
        //        { len_text = 28; }
        //        k_sg = 0.5524 + 0.0025 * len_text;
        //    }


        //    width = textSize.Width * k_sg;
        //    return width;

        //}

        //static double GetCharacterWidthPrecise(string text, float heightMm)
        //{
        //    if (string.IsNullOrEmpty(text)) return 0;

        //    // Создаём шрифт. Важно: единицы измерения (пиксели) не совпадают с мм напрямую,
        //    // но соотношение ширины будет корректным.
        //    using (Font font = new Font("ISOCPEUR", heightMm, FontStyle.Regular, GraphicsUnit.Millimeter))
        //    using (Bitmap bmp = new Bitmap(1, 1))
        //    using (Graphics g = Graphics.FromImage(bmp))
        //    {
        //        // GenericTypographic убирает лишние отступы, которые GDI добавляет для вёрстки
        //        StringFormat format = StringFormat.GenericTypographic;
        //        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces; // учитывать пробелы в конце

        //        SizeF size = g.MeasureString(text, font, int.MaxValue, format);

        //        // Возвращаем ширину в миллиметрах (т.к. шрифт задан в мм)
        //        return size.Width;
        //    }
        //}
    }
}