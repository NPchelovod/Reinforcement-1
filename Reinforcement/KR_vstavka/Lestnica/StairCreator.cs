using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;


namespace Reinforcement
{
    public  static partial class StairCreator
    {
        // Revit работает во внутренних футах. Вводимые пользователем миллиметры нужно преобразовывать: double feet = mm / 304.8;.
        public static StairParametersViewModel VM { get; set; }//данный из wpf окна
        public static Level LevelCreator { get; set; }
        public static double GZ { get; set; }//глобальная Z мм
        public static Autodesk.Revit.DB.Document Doc { get; set; }

        public static double StairLength=> VM.StairLength;
        public static double StairHeight=>VM.StairHeight;
        public static double StairWidth=>VM.StairWidth;
        public static double NumStair => (int)VM.StairNum;
        public static double StupenNum => (int)VM.StupenNum;
        public static double StupenH => StairHeight / StupenNum;
        public static double StupenL => StairLength / StupenNum;

        public static double CosourZazor = 85;
        public static double AngleTg => Math.Atan(StairHeight / StairLength);

        public static double WidthPloshadka = 1300;//ширина площадки
        public static void CreateStairComponents(StairParametersViewModel vm)
        {
            VM = vm;
            DataSymbols.Clear();
            // Здесь нужно получить текущий документ Revit через статическое поле или через ExternalCommandData.
            // Для простоты предполагаем, что вы сохранили ссылку на UIDocument в статическом свойстве перед вызовом.
            // Или передайте параметры в метод вместе с Document.

            // Пример получения документа (реализуйте сохранение ссылки):
            UIApplication uiapp = RevitAPI.UiApplication;
            if (uiapp == null) return;
            Doc = RevitAPI.Document;
            View activeView = Doc.ActiveView;
            if (activeView == null)
            {
                TaskDialog.Show("Ошибка", "Активный вид не найден. Откройте вид (план, фасад, 3D) и повторите попытку.");
                return;
            }
            //обычно у нас инженерные планы
            if(activeView.ViewType != ViewType.EngineeringPlan && activeView.ViewType!= ViewType.FloorPlan && activeView.ViewType != ViewType.CeilingPlan)
            {
                TaskDialog.Show("Ошибка", "Откройте план, для лестницы нужен план");
                return;
            }
            ViewPlan viewPlan = activeView as ViewPlan;
            if(viewPlan == null) 
            {
                TaskDialog.Show("Ошибка", "не преобразуется к плану");
                return;
            }

            LevelCreator = viewPlan.GenLevel;
            if (LevelCreator == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось определить уровень для активного вида.");
                return;
            }
            GZ = LevelCreator.Elevation*MMFromFeet;

            using (Transaction trans = new Transaction(Doc, "Создание лестницы"))
            {
                trans.Start();
                // Здесь логика:
                // 1. Загрузить/найти семейства по именам из vm
                // 2. Создать пластины с толщиной vm.PlateThickness
                // 3. Создать балки косоуры по длине и высоте
                // 4. Создать поддерживающие балки
                // 5. Создать направляющие колонны

                //сразу все семейства определяем
                //foreach (var data in vm.NamesFamilies.ToList())
                //{
                //    string name = data.Value.name;
                //    if (string.IsNullOrEmpty(name)){ continue; }
                    
                //    Element e = HelperSeach.GetExistFamily(new HashSet<string> { name }, ElementTypeOrSymbol.ElementType);
                //    vm.NamesFamilies[data.Key] = (name, e);
                //}

                CreateOpors();

                trans.Commit();
            }
        }



        //public class DataSymbol
        //{
        //    //X — направление оси балки (от startPoint к endPoint).

        //   // Y — горизонтальная(боковая) ось, перпендикулярная X.

        //    //Z — вертикальная ось (вверх), перпендикулярная X и Y.



        //    //в мм
        //    public double WidthPlaceY { get; set; } = 0;
        //    public double HeightPlaceZ { get; set; } = 0;
        //    public double ExcentrPlaceY { get; set; } = 0;
        //    public double ExcentrPlaceZ { get; set; } = 0;

        //    public double WidthVertY { get; set; } = 0;
        //    public double HeighVertX { get; set; } = 0;
        //    public double ExcentrVertY { get; set; } = 0;
        //    public double ExcentrVertX { get; set; } = 0;

        //    public double W { get; set; } = 0;//размеры сечения профиля
        //    public double H { get; set; } = 0;
        //    public DataSymbol(FamilySymbol familyType, Level levelCreator)
        //    {
        //        Autodesk.Revit.DB.Document doc = RevitAPI.Document;

        //        const double length = 10.0; // длина временной балки в футах

        //        XYZ startPoint = new XYZ(0, 0, 0);
        //        XYZ endPoint = new XYZ(length, 0, 0);
        //        Line beamCurve = Line.CreateBound(startPoint, endPoint);

        //        Element element = doc.Create.NewFamilyInstance(beamCurve, familyType, levelCreator, StructuralType.Beam);

        //        doc.Regenerate();

        //        BoundingBoxXYZ box = element.get_BoundingBox(null);  // <-- null вместо вида возврат глобального бокса
        //        if (box != null)
        //        {
        //            // Компоненты в локальных координатах (футах)

        //            // 4. Вычисляем размеры в футах
        //            WidthPlaceY = Math.Abs(box.Max.Y - box.Min.Y) * MMFromFeet;   // ширина сечения по Y
        //            HeightPlaceZ = Math.Abs(box.Max.Z - box.Min.Z) * MMFromFeet; // высота сечения по Z

        //            ExcentrPlaceY = (box.Max.Y + box.Min.Y) / 2 * MMFromFeet;//ексцентрик привязки
        //            ExcentrPlaceZ = (box.Max.Z + box.Min.Z) / 2 * MMFromFeet;//для колонн если по X
        //        }
        //        //удаляем элемент
        //        doc.Delete(element.Id);

        //        startPoint = new XYZ(0, 0, 0);
        //        endPoint = new XYZ(0, 0, length);
        //        beamCurve = Line.CreateBound(startPoint, endPoint);
        //        element = doc.Create.NewFamilyInstance(beamCurve, familyType, levelCreator, StructuralType.Beam);
        //        doc.Regenerate();
        //        box = element.get_BoundingBox(null);

        //        if (box != null)
        //        {
        //            WidthVertY = Math.Abs(box.Max.Y - box.Min.Y) * MMFromFeet;   // ширина сечения по Y
        //            HeighVertX = Math.Abs(box.Max.X - box.Min.X) * MMFromFeet; // высота сечения по Z

        //            ExcentrVertY = (box.Max.Y + box.Min.Y) / 2 * MMFromFeet;//ексцентрик привязки
        //            ExcentrVertX = (box.Max.X + box.Min.X) / 2 * MMFromFeet;//для колонн если по X

        //        }
        //        doc.Delete(element.Id);

        //        List<double> distances = new List<double>()
        //        {
        //            WidthPlaceY, HeightPlaceZ, WidthVertY, HeighVertX
        //        };
        //        distances = distances.Where(distance => distance > 0).ToList();

        //        W = distances.Min();
        //        H = distances.Max();
        //    }
        //}

        public class DataSymbol
        {
            // Размеры и эксцентриситеты в локальной системе семейства (в мм)
            public double W { get; set; } = 0;//размеры сечения профиля
            public double H { get; set; } = 0;
            public double WidthY { get; set; } = 0;   // ширина по локальной оси Y
            public double HeightZ { get; set; } = 0;  // высота по локальной оси Z
            public double ExcentrY { get; set; } = 0; // смещение центра по Y (от оси)
            public double ExcentrZ { get; set; } = 0; // смещение центра по Z

            public DataSymbol(FamilySymbol familyType, Level levelCreator)
            {
                DataSymbols[familyType] = this;
                Autodesk.Revit.DB.Document doc = RevitAPI.Document;
                const double length = 10.0; // футов

                // Создаём временный горизонтальный элемент вдоль оси X
                Line tempCurve = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(length, 0, 0));

                

                FamilyInstance element = doc.Create.NewFamilyInstance(tempCurve, familyType, levelCreator, StructuralType.Beam);
                doc.Regenerate();
                BoundingBoxXYZ box = element.get_BoundingBox(null);
                if (box != null)
                {
                    // Все значения в миллиметрах
                    WidthY = Math.Abs(box.Max.Y - box.Min.Y) * 304.8;
                    HeightZ = Math.Abs(box.Max.Z - box.Min.Z) * 304.8;
                    ExcentrY = ((box.Max.Y + box.Min.Y) / 2.0) * 304.8;
                    ExcentrZ = ((box.Max.Z + box.Min.Z) / 2.0) * 304.8;
                }

                doc.Delete(element.Id);
                
                List<double> distances = new List<double>()
                {
                    WidthY,HeightZ
                };
                distances = distances.Where(distance => distance > 0).ToList();

                W = distances.Min();
                H = distances.Max();
            }
        }





        public static Dictionary<FamilySymbol, DataSymbol> DataSymbols { get; set; } = new Dictionary<FamilySymbol, DataSymbol>();


        public static double MMFromFeet = 304.8; // из футов в мм


        //public static Element CreateElement(FamilySymbol familySymbol, Level levelCreator, XYZ startPoint, XYZ endPoint, double rotateRadians = 0)
        //{
        //    //привязка центральная 
        //    if(!DataSymbols.TryGetValue(familySymbol, out var data))
        //    {
        //        data = new DataSymbol(familySymbol, LevelCreator);
        //        DataSymbols[familySymbol] = data;
        //    }

        //    Autodesk.Revit.DB.Document doc = RevitAPI.Document;

        //    double lengthFeet = startPoint.DistanceTo(endPoint); // длина в футах


        //    XYZ direction = (endPoint - startPoint).Normalize();
        //    double angleToVertical = direction.AngleTo(XYZ.BasisZ); // радианы, 0 = строго вверх
        //    const double verticalThreshold = Math.PI / 4; // 45 градусов

        //    StructuralType sType;
        //    bool colEl = false;
        //    if (angleToVertical < verticalThreshold)
        //    {
        //        sType = StructuralType.Column; 
        //        colEl = true;  // элемент близок к вертикали
        //    }
        //    else
        //    {
        //        sType = StructuralType.Beam;      // элемент близок к горизонтали
        //    }
        //    sType = StructuralType.Beam;

        //    Line Curve = Line.CreateBound(startPoint, endPoint);
        //    FamilyInstance element = doc.Create.NewFamilyInstance(Curve, familySymbol, levelCreator, sType);

        //    if(!colEl)
        //    {
        //        if (Math.Abs(data.ExcentrPlaceY) > 0.001 || Math.Abs(data.ExcentrPlaceZ) > 0.001)
        //        {
        //            XYZ Offset = new XYZ(0, -data.ExcentrPlaceY/MMFromFeet, -data.ExcentrPlaceZ / MMFromFeet);
        //            ElementTransformUtils.MoveElement(doc, element.Id, Offset);
        //            doc.Regenerate();
        //        }
        //    }
        //    else
        //    {
        //        if (Math.Abs(data.ExcentrVertX) > 0.001 || Math.Abs(data.ExcentrVertY) > 0.001)
        //        {
        //            XYZ Offset = new XYZ(-data.ExcentrVertX / MMFromFeet, -data.ExcentrVertY / MMFromFeet, 0);
        //            ElementTransformUtils.MoveElement(doc, element.Id, Offset);
        //            doc.Regenerate();
        //        }
        //    }

        //    // 5. Если требуется дополнительный поворот сечения вокруг оси элемента (например, двутавр на 90°)
        //    if (Math.Abs(rotateRadians) > 0.001)
        //    {
        //        Line elementAxis = Line.CreateBound(startPoint, endPoint);
        //        ElementTransformUtils.RotateElement(doc, element.Id, elementAxis, rotateRadians);
        //        doc.Regenerate();
        //    }

        //    return element;
        //}
        public static Dictionary<ElementId, Line> ElementCentralAxes { get; set; } = new Dictionary<ElementId, Line>();
        public static Element CreateElement(FamilySymbol familySymbol, Level levelCreator,
                                    XYZ startPoint, XYZ endPoint, double rotateRadians = 0)
        {
            // Получаем или вычисляем данные о сечении
            if (!DataSymbols.TryGetValue(familySymbol, out var data))
            {
                data = new DataSymbol(familySymbol, levelCreator);
                DataSymbols[familySymbol] = data;
            }

            Autodesk.Revit.DB.Document  doc = RevitAPI.Document;

            // Определяем тип элемента (балка или колонна) по углу наклона
            XYZ direction = (endPoint - startPoint).Normalize();
            double angleToVertical = direction.AngleTo(XYZ.BasisZ);
            const double verticalThreshold = Math.PI / 4; // 45 градусов
            StructuralType sType = (angleToVertical < verticalThreshold)
                                    ? StructuralType.Column
                                    : StructuralType.Beam;
            sType = StructuralType.Beam;
            // Создаём элемент по заданной кривой
            Line curve = Line.CreateBound(startPoint, endPoint);
            FamilyInstance element = doc.Create.NewFamilyInstance(curve, familySymbol, levelCreator, sType);
            doc.Regenerate();

            // Компенсация эксцентриситета (если есть)
            if (Math.Abs(data.ExcentrY) > 0.001 || Math.Abs(data.ExcentrZ) > 0.001)
            {
                Transform trans = element.GetTotalTransform();
                if (trans != null)
                {
                    // Вектор смещения в локальной системе семейства (футы)
                    // Знак минус – чтобы вернуть геометрический центр на ось
                    XYZ localOffset = new XYZ(0, -data.ExcentrY / 304.8, -data.ExcentrZ / 304.8);
                    XYZ globalOffset = trans.OfVector(localOffset);
                    ElementTransformUtils.MoveElement(doc, element.Id, globalOffset);
                    doc.Regenerate();
                }
            }
            ElementCentralAxes[element.Id] = Line.CreateBound(startPoint, endPoint);
            // Дополнительный поворот сечения вокруг оси элемента
            if (Math.Abs(rotateRadians) > 0.001)
            {
                Line centralAxis = ElementCentralAxes[element.Id];
                ElementTransformUtils.RotateElement(doc, element.Id, centralAxis, rotateRadians);
                doc.Regenerate();
                // Обновляем центральную ось после поворота
                ElementCentralAxes[element.Id] = TransformLine(centralAxis, rotateRadians);
            }

            return element;
        }
        private static Line TransformLine(Line line, double angleRadians)
        {
            XYZ start = line.GetEndPoint(0);
            XYZ end = line.GetEndPoint(1);
            XYZ dir = line.Direction;
            Transform rot = Transform.CreateRotationAtPoint(dir, angleRadians, start);
            XYZ newStart = rot.OfPoint(start);
            XYZ newEnd = rot.OfPoint(end);
            return Line.CreateBound(newStart, newEnd);
        }
        public static Element CreateMoveElement(Element element, XYZ translation,  double rotateRadians = 0)
        {
            if (element == null) { return null; }
            Autodesk.Revit.DB.Document doc = RevitAPI.Document;
            

            ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElement(doc, element.Id, translation);
            if (copiedIds == null || !copiedIds.Any())
            {
                // Обработка ошибки: элемент не скопирован
                
                return null;
            }
            ElementId newId = copiedIds.First();

            if (newId == null) {return null;}
            Element newElement = doc.GetElement(newId);

            if(ElementCentralAxes.TryGetValue(element.Id, out Line centerLine))
            {
                XYZ startPoint = centerLine.GetEndPoint(0);
                XYZ endPoint = centerLine.GetEndPoint(1);
                startPoint = new XYZ(startPoint.X + translation.X, startPoint.Y + translation.Y, startPoint.Z + translation.Z);
                endPoint = new XYZ(endPoint.X + translation.X, endPoint.Y + translation.Y, endPoint.Z + translation.Z);
                ElementCentralAxes[newElement.Id] = Line.CreateBound(startPoint, endPoint);
            }
            

            if (Math.Abs(rotateRadians) < 0.001 || newElement ==null)
            {
                doc.Regenerate();
                return newElement;
            }

            RotateElement(newElement, rotateRadians);

            return newElement;

        }

        public static Element RotateElement(Element element, double rotateRadians)
        {
            if(element == null) { return null;}
            Autodesk.Revit.DB.Document doc = element.Document;
            //FamilyInstance beam = element as FamilyInstance;
            //if (beam == null) return element;

            //// Получаем текущую кривую элемента
            //LocationCurve locCurve = beam.Location as LocationCurve;
            //if (locCurve == null) return element;
            //Line currentLine = locCurve.Curve as Line;
            //if (currentLine == null) return element;

            //XYZ startPoint = currentLine.GetEndPoint(0);
            //XYZ endPoint = currentLine.GetEndPoint(1);

            if (ElementCentralAxes.TryGetValue(element.Id, out Line centralAxis))
            {  // или вычислить заново

                // Если поворот на 180° (с допустимой погрешностью)
                if (Math.Abs(rotateRadians - Math.PI) < 0.001 || Math.Abs(rotateRadians + Math.PI) < 0.001)
                {

                    // Пересоздаём элемент с обратным направлением
                    //тут должны быть другие точки - центр сечения
                    XYZ start = centralAxis.GetEndPoint(0);
                    XYZ end = centralAxis.GetEndPoint(1);
                    FamilyInstance beam = element as FamilyInstance;
                    Element newElement = CreateElement(beam.Symbol, LevelCreator, end, start, 0);
                    ElementCentralAxes.Remove(element.Id);
                    doc.Delete(element.Id);
                    doc.Regenerate();
                    return newElement;

                }
            }

            // Для остальных углов: поворачиваем вокруг оси элемента
           
                // Ось вращения – текущая линия элемента (рабочая ось)
             ElementTransformUtils.RotateElement(doc, element.Id, centralAxis, rotateRadians);//так верно
            doc.Regenerate();

            return element;
        }
        public static Line GetCentralAxis(Element element)
        {
            Autodesk.Revit.DB.Document doc = element.Document;
            Options opt = new Options();
            opt.DetailLevel = ViewDetailLevel.Fine;
            GeometryElement geoElem = element.get_Geometry(opt);

            List<Face> endFaces = new List<Face>();
            XYZ direction = null;

            // Получаем текущую LocationCurve как приблизительное направление
            LocationCurve locCurve = element.Location as LocationCurve;
            if (locCurve != null && locCurve.Curve is Line line)
                direction = line.Direction;
            else
                direction = XYZ.BasisX; // запасной вариант

            // Обходим геометрию, ищем грани, перпендикулярные направлению (торцы)
            foreach (GeometryObject geomObj in geoElem)
            {
                if (geomObj is GeometryInstance geomInst)
                {
                    GeometryElement instGeom = geomInst.GetInstanceGeometry();
                    foreach (GeometryObject instObj in instGeom)
                    {
                        if (instObj is Solid solid && solid.Volume > 0)
                        {
                            foreach (Face face in solid.Faces)
                            {
                                XYZ normal = face.ComputeNormal(new UV(0.5, 0.5));
                                // Если нормаль почти параллельна направлению элемента -> это торец
                                if (Math.Abs(normal.DotProduct(direction)) > 0.9)
                                {
                                    endFaces.Add(face);
                                }
                            }
                        }
                    }
                }
            }

            if (endFaces.Count < 2)
                return null;

            // Вычисляем центры торцевых граней
            XYZ center1 = GetFaceCenter(endFaces[0]);
            XYZ center2 = GetFaceCenter(endFaces[1]);

            // Сортируем по расстоянию от начала координат (необязательно)
            // Но главное - создать линию
            return Line.CreateBound(center1, center2);
        }

        // Вспомогательный метод: центр грани (среднее арифметическое точек её периметра)
        private static XYZ GetFaceCenter(Face face)
        {
            Mesh mesh = face.Triangulate();
            XYZ sum = XYZ.Zero;
            int count = 0;
            foreach (XYZ pt in mesh.Vertices)
            {
                sum += pt;
                count++;
            }
            return sum / count;
        }
        


        

    }
}
