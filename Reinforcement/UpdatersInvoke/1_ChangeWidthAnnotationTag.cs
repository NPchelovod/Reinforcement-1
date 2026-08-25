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
//using System.Windows;

namespace Updaters
{
    public class ChangeWidthAnnotationTag : IUpdater
    {

        public static bool ChangeMaskField = false;

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
        private DateTime now;
        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            //var ids = data.GetModifiedElementIds();
            var ids = data.GetModifiedElementIds().ToList(); // ✅ Материализовать!

            //bool functProxod = false;
            //var pastElements2 = new HashSet<Element>();
            if (pastElements.Count > 1000)
            {
                lock (pastElements) { pastElements.Clear(); }
            }

            now = DateTime.UtcNow; // ✅ Фиксируем время СНАРУЖИ

            foreach (var id in ids)
            {
                

                bool shouldProcess;
                lock (pastElements) // ✅ Короткий lock только для проверки
                {
                    shouldProcess = !pastElements.TryGetValue(id, out var date)
                                 || (now - date).TotalSeconds >= updateTime;
                }

                if (!shouldProcess) 
                { 
                    continue; 
                }

                var element = doc.GetElement(id);

                // два раза чтобы не входила сама в себя рекурсией

                if (shrift(doc, element, id))
                {
                    //два раза не надо лезть туда
                    continue;
                } // корректировка выносок

            }
            
            
            //pastElements = pastElements2;

        }

        private static double updateTime = 1.1;

        private static List<string> nameWidths = new List<string> { "ЕС_Ширина полки", "Ширина полки" };
        
        private static Dictionary<ElementId, DateTime> pastElements = new Dictionary<ElementId, DateTime>();

        private bool shrift(Document doc,Element element, ElementId elementId)
        {
            try
            {
                string name = element.LookupParameter("Семейство и типоразмер").AsValueString();

                if (!name.Contains("Выноска") && !name.Contains("выноска")) { return false; }
                
                lock (pastElements) // ✅ Короткий lock 
                {
                    pastElements[elementId] = now;
                }

                string firstText = element.LookupParameter("Текст верх").AsString();
                string secondText = element.LookupParameter("Текст низ").AsString();
                var text = firstText.Count() > secondText.Count() ? firstText : secondText;
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
                double width = Math.Max(width1, width2);
                

                foreach(var wid in nameWidths)// возможные имена
                {
                    Parameter paramW = element.LookupParameter(wid);
                    if (paramW != null)
                    {
                        paramW.Set(width);
                        break;
                    }
                }
                
                Parameter param = element.LookupParameter("ЕС_Ширина текста верх");
                if (param != null && !param.IsReadOnly)
                { 
                    param.Set(width1);
                    param = element.LookupParameter("ЕС_Ширина текста низ");
                    if (param != null && !param.IsReadOnly)
                    { param.Set(width2); }
                }

                //TaskDialog.Show("Revit updater",$"Имя измененного элемента {element.Name}");
                if (ChangeMaskField)
                {
                    //maskirovka(doc, element);
                }

            }
            catch  { return false; }
            return true;
        }



        private void maskirovka(Document doc, Element element)
        {
            try
            {
                // 1. ПОЛУЧАЕМ ID СВЯЗАННОЙ МАСКИРОВКИ ИЗ ПАРАМЕТРА ВЫНОСКИ
                // 1. ПОДГОТОВКА: Получаем ID старой области и находим ВИД
                
                // 2. СОЗДАЁМ НОВУЮ МАСКИРОВКУ
                // Находим вид, на котором находится выноска (активный или через поиск)
                Autodesk.Revit.DB.View activeView = doc.ActiveView;
                BoundingBoxXYZ elementBBox = element.get_BoundingBox(activeView);
                if (elementBBox == null) return;
                // 2. Получаем ID старой связанной маскировки из параметра выноски
                Parameter linkParam = element.LookupParameter("ЕС_ID связанного элемента");
                //if (linkParam == null) return;
                string oldRegionIdString = linkParam?.AsValueString();
                ElementId oldRegionId = null;

                using (Transaction trans = new Transaction(doc, "Обновить маскировку"))
                {
                    trans.Start();

                    // 2. УДАЛЕНИЕ: Удаляем старую область, если она существует
                    if (!string.IsNullOrEmpty(oldRegionIdString) && int.TryParse(oldRegionIdString, out int idInt))
                    {
                        oldRegionId = new ElementId(idInt);
                        Element oldRegion = doc.GetElement(oldRegionId);
                        // Удаляем ТОЛЬКО если это область маскировки (DetailComponent)
                        
                        doc.Delete(oldRegionId);
                        
                    }

                    // 3. СОЗДАНИЕ: Получаем актуальные границы и создаём новую область
                    // Обновляем модель для актуальных границ
                    doc.Regenerate();
                    BoundingBoxXYZ bbox = element.get_BoundingBox(activeView);
                    if (bbox == null) { trans.Commit(); return; }

                    // Получаем координаты с учётом преобразования системы координат[citation:1]
                    Transform trf = bbox.Transform;
                    XYZ minPoint = trf.OfPoint(bbox.Min);
                    XYZ maxPoint = trf.OfPoint(bbox.Max);

                    // Создаём контур по границам
                    CurveLoop newCurveLoop = CreateRectangularCurveLoop(minPoint, maxPoint);
                    //IList<CurveLoop> curveLoops = new List<CurveLoop> { newCurveLoop };
                    // Создаём прямоугольный контур по границам BoundingBox
                    CurveLoop curveLoop = new CurveLoop();
                    // Нижняя грань
                    curveLoop.Append(Line.CreateBound(
                        new XYZ(minPoint.X, minPoint.Y, 0),
                        new XYZ(maxPoint.X, minPoint.Y, 0)));
                    // Правая грань
                    curveLoop.Append(Line.CreateBound(
                        new XYZ(maxPoint.X, minPoint.Y, 0),
                        new XYZ(maxPoint.X, maxPoint.Y, 0)));
                    // Верхняя грань
                    curveLoop.Append(Line.CreateBound(
                        new XYZ(maxPoint.X, maxPoint.Y, 0),
                        new XYZ(minPoint.X, maxPoint.Y, 0)));
                    // Левая грань (замыкаем контур)
                    curveLoop.Append(Line.CreateBound(
                        new XYZ(minPoint.X, maxPoint.Y, 0),
                        new XYZ(minPoint.X, minPoint.Y, 0)));

                    // 6. Получаем тип области заливки (FilledRegionType)
                    // Области заливки относятся к категории OST_FilledRegions
                    FilteredElementCollector collector = new FilteredElementCollector(doc);
                    FilledRegionType filledRegionType = collector
                        .OfClass(typeof(FilledRegionType))
                        .Cast<FilledRegionType>()
                        .FirstOrDefault(ft => ft.ForegroundPatternColor.IsValid);

                    // НАХОДИМ ТИП ОБЛАСТИ: Ищем FamilySymbol для категории Detail Components[citation:6]
                    //FilteredElementCollector collector = new FilteredElementCollector(doc);
                    //FamilySymbol maskingRegionType = collector
                    //    .OfClass(typeof(FamilySymbol))
                    //    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    //    .Cast<FamilySymbol>()
                    //    .FirstOrDefault(sym => sym.FamilyName.Contains("Маскировка") || sym.Name.Contains("Маскировка"));
                    // Если тип не найден, попробуем найти через имя
                    if (filledRegionType == null)
                    {
                        filledRegionType = collector
                            .OfClass(typeof(FilledRegionType))
                            .Cast<FilledRegionType>()
                            .FirstOrDefault(ft => ft.Name.Contains("Сплошная") || ft.Name.Contains("Solid"));
                    }

                    if (filledRegionType == null)
                    {
                        Debug.WriteLine("Не удалось найти тип области заливки (FilledRegionType) в проекте.");
                        trans.Commit();
                        return;
                    }


                    FilledRegion newFilledRegion = FilledRegion.Create(doc, filledRegionType.Id, activeView.Id, new List<CurveLoop> { curveLoop });

                    // 9. Сохраняем ID новой области в параметр выноски для будущих обновлений
                    if (linkParam != null && !linkParam.IsReadOnly)
                    {
                        linkParam.Set(newFilledRegion.Id.IntegerValue.ToString());
                    }
                    
                    trans.Commit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка в maskirovka: {ex.Message}");
                return;
            }
        }
        
        
        // Вспомогательная функция для создания прямоугольного контура
        private CurveLoop CreateRectangularCurveLoop(XYZ min, XYZ max)
        {
            CurveLoop loop = new CurveLoop();
            // Создаем прямоугольник по 4 точкам
            loop.Append(Line.CreateBound(min, new XYZ(max.X, min.Y, 0)));
            loop.Append(Line.CreateBound(new XYZ(max.X, min.Y, 0), max));
            loop.Append(Line.CreateBound(max, new XYZ(min.X, max.Y, 0)));
            loop.Append(Line.CreateBound(new XYZ(min.X, max.Y, 0), min));
            return loop;
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
