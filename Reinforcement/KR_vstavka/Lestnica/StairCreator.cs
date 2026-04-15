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
using static Reinforcement.StairCreator;

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

            Level LevelCreator = viewPlan.GenLevel;
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



        public class DataSymbol
        {
            //X — направление оси балки (от startPoint к endPoint).

           // Y — горизонтальная(боковая) ось, перпендикулярная X.

            //Z — вертикальная ось (вверх), перпендикулярная X и Y.

           
            
            //в мм
            public double WidthPlaceY { get; set; } = 0;
            public double HeightPlaceZ { get; set; } = 0;
            public double ExcentrPlaceY { get; set; } = 0;
            public double ExcentrPlaceZ { get; set; } = 0;

            public double WidthVertY { get; set; } = 0;
            public double HeighVertX { get; set; } = 0;
            public double ExcentrVertY { get; set; } = 0;
            public double ExcentrVertX { get; set; } = 0;

            public double W { get; set; } = 0;//размеры сечения профиля
            public double H { get; set; } = 0;
            public DataSymbol(FamilySymbol familyType, Level levelCreator)
            {
                Autodesk.Revit.DB.Document doc = RevitAPI.Document;

                const double length = 10.0; // длина временной балки в футах

                XYZ startPoint = new XYZ(0, 0, 0);
                XYZ endPoint = new XYZ(length, 0, 0);
                Line beamCurve = Line.CreateBound(startPoint, endPoint);

                Element element = doc.Create.NewFamilyInstance(beamCurve, familyType, levelCreator, StructuralType.Beam);

                doc.Regenerate();

                BoundingBoxXYZ box = element.get_BoundingBox(null);  // <-- null вместо вида возврат глобального бокса
                if (box != null)
                {
                    // Компоненты в локальных координатах (футах)

                    // 4. Вычисляем размеры в футах
                    WidthPlaceY = Math.Abs(box.Max.Y - box.Min.Y) * MMFromFeet;   // ширина сечения по Y
                    HeightPlaceZ = Math.Abs(box.Max.Z - box.Min.Z) * MMFromFeet; // высота сечения по Z

                    ExcentrPlaceY = (box.Max.Y + box.Min.Y) / 2 * MMFromFeet;//ексцентрик привязки
                    ExcentrPlaceZ = (box.Max.Z + box.Min.Z) / 2 * MMFromFeet;//для колонн если по X
                }
                //удаляем элемент
                doc.Delete(element.Id);

                startPoint = new XYZ(0, 0, 0);
                endPoint = new XYZ(0, 0, length);
                beamCurve = Line.CreateBound(startPoint, endPoint);
                element = doc.Create.NewFamilyInstance(beamCurve, familyType, levelCreator, StructuralType.Beam);
                doc.Regenerate();
                box = element.get_BoundingBox(null);

                if (box != null)
                {
                    WidthVertY = Math.Abs(box.Max.Y - box.Min.Y) * MMFromFeet;   // ширина сечения по Y
                    HeighVertX = Math.Abs(box.Max.X - box.Min.X) * MMFromFeet; // высота сечения по Z

                    ExcentrVertY = (box.Max.Y + box.Min.Y) / 2 * MMFromFeet;//ексцентрик привязки
                    ExcentrVertX = (box.Max.X + box.Min.X) / 2 * MMFromFeet;//для колонн если по X

                }
                doc.Delete(element.Id);

                List<double> distances = new List<double>()
                {
                    WidthPlaceY, HeightPlaceZ, WidthVertY, HeighVertX
                };
                distances = distances.Where(distance => distance > 0).ToList();

                W = distances.Min();
                H = distances.Max();
            }
        }

        public static Dictionary<FamilySymbol, DataSymbol> DataSymbols { get; set; } = new Dictionary<FamilySymbol, DataSymbol>();


        public static double MMFromFeet = 304.8; // из футов в мм


        public static Element CreateElement(FamilySymbol familySymbol, Level levelCreator, XYZ startPoint, XYZ endPoint, double rotateRadians = 0)
        {
            //привязка центральная 
            if(!DataSymbols.TryGetValue(familySymbol, out var data))
            {
                data = new DataSymbol(familySymbol, LevelCreator);
                DataSymbols[familySymbol] = data;
            }

            Autodesk.Revit.DB.Document doc = RevitAPI.Document;

            double lengthFeet = startPoint.DistanceTo(endPoint); // длина в футах


            XYZ direction = (endPoint - startPoint).Normalize();
            double angleToVertical = direction.AngleTo(XYZ.BasisZ); // радианы, 0 = строго вверх
            const double verticalThreshold = Math.PI / 4; // 45 градусов

            StructuralType sType;
            bool colEl = false;
            if (angleToVertical < verticalThreshold)
            {
                sType = StructuralType.Column; 
                colEl = true;  // элемент близок к вертикали
            }
            else
            {
                sType = StructuralType.Beam;      // элемент близок к горизонтали
            }
            sType = StructuralType.Beam;

            Line Curve = Line.CreateBound(startPoint, endPoint);
            FamilyInstance element = doc.Create.NewFamilyInstance(Curve, familySymbol, levelCreator, sType);

            if(!colEl)
            {
                if (Math.Abs(data.ExcentrPlaceY) > 0.001 || Math.Abs(data.ExcentrPlaceZ) > 0.001)
                {
                    XYZ Offset = new XYZ(0, -data.ExcentrPlaceY/MMFromFeet, -data.ExcentrPlaceZ / MMFromFeet);
                    ElementTransformUtils.MoveElement(doc, element.Id, Offset);
                    doc.Regenerate();
                }
            }
            else
            {
                if (Math.Abs(data.ExcentrVertX) > 0.001 || Math.Abs(data.ExcentrVertY) > 0.001)
                {
                    XYZ Offset = new XYZ(-data.ExcentrVertX / MMFromFeet, -data.ExcentrVertY / MMFromFeet, 0);
                    ElementTransformUtils.MoveElement(doc, element.Id, Offset);
                    doc.Regenerate();
                }
            }

            // 5. Если требуется дополнительный поворот сечения вокруг оси элемента (например, двутавр на 90°)
            if (Math.Abs(rotateRadians) > 0.001)
            {
                Line elementAxis = Line.CreateBound(startPoint, endPoint);
                ElementTransformUtils.RotateElement(doc, element.Id, elementAxis, rotateRadians);
                doc.Regenerate();
            }

            return element;
        }

        public static Element CreateMoveElement(Element element, XYZ translation,  double rotateRadians = 0)
        {
            Autodesk.Revit.DB.Document doc = RevitAPI.Document;
            var newEId=  ElementTransformUtils.CopyElement(
            doc, element.Id, translation).FirstOrDefault();
            if(newEId ==null) {return null;}
            Element newElement = doc.GetElement(newEId);

            if (Math.Abs(rotateRadians) < 0.001 || newElement ==null)
            {
                return newElement;
            }

            RotateElement(newElement, rotateRadians);

            return newElement;

        }

        public static Element RotateElement(Element element, double rotateRadians = 0)
        {
            // Получаем текущую Curve (рабочая ось)
            LocationCurve locCurve = element.Location as LocationCurve;
            Line line = null;
            XYZ dir = null;
            if (locCurve != null)
            {
                if (locCurve.Curve != null)
                {
                    line = locCurve.Curve as Line;
                    if (line != null)
                    {
                        dir = line.Direction;
                    }
                }
            }
            if (dir == null)
            {

                return element;
            }
            Autodesk.Revit.DB.Document doc = RevitAPI.Document;
            FamilyInstance beam = element as FamilyInstance;
            FamilySymbol familySymbol = beam.Symbol;
            if (!DataSymbols.TryGetValue(familySymbol, out var data))
            {
                data = new DataSymbol(familySymbol, LevelCreator);
                DataSymbols[familySymbol] = data;
            }

            //находим истинную ось элемента вокруг которой и вертим
            double angleToVertical = dir.AngleTo(XYZ.BasisZ); // радианы, 0 = строго вверх
            const double verticalThreshold = Math.PI / 4; // 45 градусов


            XYZ startPoint = line.GetEndPoint(0);  // Начальная точка
            XYZ endPoint = line.GetEndPoint(1);    // Конечная точка

            bool colEl = false;

            XYZ startPointAxe = null;
            XYZ endPointAxe = null;
            if (angleToVertical < verticalThreshold)
            {
                colEl = true;  // элемент близок к вертикали
                startPointAxe = new XYZ(startPoint.X, startPoint.Y + data.ExcentrPlaceY / MMFromFeet, startPoint.Z + data.ExcentrPlaceZ / MMFromFeet);
                endPointAxe = new XYZ(endPoint.X, endPoint.Y + data.ExcentrPlaceY / MMFromFeet, endPoint.Z);
            }
            else
            {
                startPointAxe = new XYZ(startPoint.X + data.ExcentrVertX / MMFromFeet, startPoint.Y + data.ExcentrVertY / MMFromFeet, startPoint.Z);
                endPointAxe = new XYZ(endPoint.X + data.ExcentrVertX / MMFromFeet, endPoint.Y + data.ExcentrVertY / MMFromFeet, endPoint.Z);
            }

            if(Math.Abs(rotateRadians- Math.PI)<0.001)
            {
                //надежнее пересоздать элемент с начала в конец
                Element newElement = CreateElement(familySymbol, LevelCreator, endPointAxe, startPointAxe);

                doc.Delete(element.Id);
                element = newElement;
                return element;
            }

            Line Curve = Line.CreateBound(startPointAxe, endPointAxe);

            ElementTransformUtils.RotateElement(doc, element.Id, Curve, rotateRadians);
            //doc.Regenerate();
            return element;
        }

        public static void  CreateOpors()
        {
            //поиск семейства по типоразмеру
            (string nameFamily, Element element) = VM.NamesFamilies[LestnicaData.Kolonn];
            element = HelperSeach.GetExistFamily(new HashSet<string> { nameFamily }, ElementTypeOrSymbol.ElementType);
            VM.NamesFamilies[LestnicaData.Kolonn] = (nameFamily, element);

            if (element == null)
            {
                return;
            }
            FamilySymbol symbol = element as FamilySymbol;
            if (symbol == null) { return; }


            if (!DataSymbols.TryGetValue(symbol, out Column))
            {
                Column = new DataSymbol(symbol, LevelCreator);
                DataSymbols[symbol] = Column;
            }


            //в миллиметрах
            //на виде сбоку это ширина сечения и высота
            //на плане это размер по Y, затем по X

           


            //// Revit работает во внутренних футах. Вводимые пользователем миллиметры нужно преобразовывать: double feet = mm / 304.8;.
            XYZ startPoint1 = new XYZ(Column.W/2 / MMFromFeet,0, GZ / MMFromFeet);
            XYZ endPoint1 = new XYZ(Column.W / 2 / MMFromFeet, 0, ColumnTopZ / MMFromFeet);
            //CreateElement(symbol, LevelCreator, new XYZ(0, 0, 0), new XYZ(0, 0, 2));
            //CreateElement(symbol, LevelCreator, new XYZ(0, 0, 0), new XYZ(0, 0, 2), Math.PI / 2);

            CreateElement(symbol, LevelCreator, startPoint1, endPoint1, Math.PI / 2);


            Column2EndX = (StupenL + StairLength);//задняя часть колонны
            double Column2CenterX = Column2EndX - Column.W / 2;

            XYZ startPoint2 = new XYZ(Column2CenterX / MMFromFeet, 0, GZ / MMFromFeet);
            XYZ endPoint2 = new XYZ(Column2CenterX / MMFromFeet, 0, ColumnTopZ / MMFromFeet);

            CreateElement(symbol, LevelCreator, startPoint2, endPoint2);
            CreateElement(symbol, LevelCreator, startPoint2, endPoint2, Math.PI / 2);

            CreateCosours();
        }


        


        public static double ColumnTopZ = 0;//футы
        public static double Column2EndX = 0;//край второй колонны прям задний край
        public static DataSymbol Column;
        public static DataSymbol Cosour;
        public static void CreateCosours()
        {
            (string nameFamily, Element element) = VM.NamesFamilies[LestnicaData.BeamCosour];
            element = HelperSeach.GetExistFamily(new HashSet<string> { nameFamily }, ElementTypeOrSymbol.ElementType);
            VM.NamesFamilies[LestnicaData.BeamCosour] = (nameFamily, element);

            if (element == null)
            {
                return;
            }
            FamilySymbol symbol = element as FamilySymbol;
            if (symbol == null) { return; }

            if (!DataSymbols.TryGetValue(symbol, out Cosour))
            {
                Cosour = new DataSymbol(symbol, LevelCreator);
                DataSymbols[symbol] = Cosour;
            }





            //смещения для косоура центра чтобы его них оказался как раз
            double dx = Cosour.H/ 2 * Math.Sin(AngleTg);
            double dz = Cosour.H / 2 * Math.Cos(AngleTg);

            double yBorder = -(Cosour.W + Column.H) / 2;

            XYZ startPoint1 = new XYZ(-dx / MMFromFeet, yBorder / MMFromFeet, (dz + GZ) / MMFromFeet);


            //для кончика косоура
            double xBorder = -dx + Column2EndX;
            double zBroder = (xBorder - startPoint1.X * MMFromFeet) * Math.Tan(AngleTg);

            XYZ endPoint1 = new XYZ(xBorder / MMFromFeet, yBorder / MMFromFeet, startPoint1.Z + zBroder / MMFromFeet);


            Line beamCurve1 = Line.CreateBound(startPoint1, endPoint1);
           
            Element cosour1 = CreateElement(symbol, LevelCreator, startPoint1, endPoint1);


            cosour1 = RotateElement(cosour1, Math.PI);


            //тест
            XYZ startPoint = new XYZ(startPoint1.X, 0, startPoint1.Z);
            XYZ endPoint = new XYZ(endPoint1.X, 0, endPoint1.Z);
            Element cosour = CreateElement(symbol, LevelCreator, startPoint, endPoint);
            Element cosour22 = CreateElement(symbol, LevelCreator, startPoint, endPoint);
            cosour22 = RotateElement(cosour22, Math.PI);







            XYZ translation = new XYZ(0, (-StairWidth - Cosour.W) / MMFromFeet, 0);
            Element cosour2 = CreateMoveElement(cosour1, translation, 0);
            cosour2 = RotateElement(cosour2, Math.PI);


            //ElementTransformUtils.RotateElement(Doc, cosour1.Id, beamCurve1, Math.PI);

            //XYZ translation = new XYZ(0, (-StairWidth - Cosour.W) / MMFromFeet, 0);




            //Element cosour2 = CreateMoveElement(cosour1, translation, Math.PI);



            //ElementTransformUtils.CopyElement(
            //Doc, cosour1.Id, translation);

            //ElementTransformUtils.RotateElement(Doc, cosour1.Id, beamCurve1, Math.PI);

        }


        

    }
}
