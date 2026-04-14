using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
            public BoundingBoxXYZ BoundingBox { get; set; }
            //в мм
            public double XWidth { get; set; } = 0;
            public double YHeight { get; set; } = 0;
            public double Excentr { get; set; } = 0;
            public double Excentr2 { get; set; } = 0;
            public DataSymbol(FamilySymbol familyType)
            {
                Autodesk.Revit.DB.Document doc = RevitAPI.Document;
                XYZ startPoint = new XYZ(0, 0, 0);
                XYZ endPoint = new XYZ(10, 0, 0);
                Line beamCurve = Line.CreateBound(startPoint, endPoint);
                Element element = doc.Create.NewFamilyInstance(beamCurve, familyType, LevelCreator, StructuralType.Beam);
                doc.Regenerate();
                BoundingBox = element.get_BoundingBox(null);  // <-- null вместо вида возврат глобального бокса
                var box = BoundingBox;
                if (box != null)
                {
                    // 4. Вычисляем размеры в футах
                    XWidth = Math.Abs(box.Max.Y - box.Min.Y)* MMFromFeet;   // ширина сечения по Y
                    YHeight = Math.Abs(box.Max.Z - box.Min.Z) * MMFromFeet; // высота сечения по Z

                    Excentr = (box.Max.Y + box.Min.Y)/2 * MMFromFeet;//ексцентрик привязки
                    Excentr2 = (box.Max.Z + box.Min.Z)/2 * MMFromFeet;//для колонн если по X
                }

                //удаляем элемент
                doc.Delete(element.Id);
            }
        }

        public static Dictionary<FamilySymbol, DataSymbol> DataSymbols { get; set; } = new Dictionary<FamilySymbol, DataSymbol>();


        public static double MMFromFeet = 304.8; // из футов в мм


        public static Element CreateElement(FamilySymbol familySymbol, XYZ startPoint, XYZ endPoint, double rotate=0)
        {
            //привязка центральная 
            if(!DataSymbols.TryGetValue(familySymbol, out var data) || data.BoundingBox==null)
            {
                data = new DataSymbol(familySymbol);
                DataSymbols[familySymbol] = data;
            }
            Line beamCurve = Line.CreateBound(startPoint, endPoint);
            Element colonn = Doc.Create.NewFamilyInstance(beamCurve, familySymbol, LevelCreator, StructuralType.Beam);

            //ищем центральную линию
            XYZ center1 = startPoint;
            XYZ center2 = endPoint;
            Line center = beamCurve;

            if(data.BoundingBox!=null)
            {
                var box = data.BoundingBox;

                //фактическая центральная линия
                center1 = new XYZ(startPoint.X + data.Excentr/ MMFromFeet, startPoint.Y + data.Excentr2 / MMFromFeet, startPoint.Z);
                center2 = new XYZ(endPoint.X + data.Excentr / MMFromFeet, endPoint.Y + data.Excentr2 / MMFromFeet, endPoint.Z);

                center = Line.CreateBound(center1, center2);

                double xError = center1.X - startPoint.X;
                double yError = center1.Y - startPoint.Y;
                if (Math.Abs(xError) > 0.001 || Math.Abs(yError) > 0.001)
                {
                    ElementTransformUtils.MoveElement(Doc, colonn.Id, new XYZ(-xError, -yError, 0));
                }

                center = beamCurve;
            }


            if(Math.Abs(rotate) > 0.001)
            {
                //поворачиваем относительно центральной линии
                ElementTransformUtils.RotateElement(Doc, colonn.Id, center, rotate);
            }
            //доводим до центров
            //находим разницу

            



            
            return colonn;
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


            if (!DataSymbols.TryGetValue(symbol, out var data))
            {
                data = new DataSymbol(symbol);
                DataSymbols[symbol] = data;
            }


            //в миллиметрах
            //на виде сбоку это ширина сечения и высота
            //на плане это размер по Y, затем по X
            



            double Xcol1 = data.XWidth / 2;
            //// Revit работает во внутренних футах. Вводимые пользователем миллиметры нужно преобразовывать: double feet = mm / 304.8;.
            XYZ startPoint1 = new XYZ(Xcol1 / MMFromFeet,0, GZ / MMFromFeet);
            XYZ endPoint1 = new XYZ(Xcol1 / MMFromFeet, 0, ColumnTopZ / MMFromFeet);

            CreateElement(symbol, startPoint1, endPoint1);
            CreateElement(symbol, startPoint1, endPoint1, Math.PI / 2);


            Column2EndX = (StupenL + StairLength);//задняя часть колонны
            double Column2CenterX = Column2EndX - Xcol1;

            XYZ startPoint2 = new XYZ(Column2CenterX / MMFromFeet, 0, GZ / MMFromFeet);
            XYZ endPoint2 = new XYZ(Column2CenterX / MMFromFeet, 0, ColumnTopZ / MMFromFeet);

            CreateElement(symbol, startPoint2, endPoint2);
            CreateElement(symbol, startPoint2, endPoint2, Math.PI / 2);


            //string nameFamily = 


            //строим сетку опор Z

            //строим опорные линии

            //теперь строим косоуры..
            CreateCosours();
        }


        


        public static double ColumnTopZ = 0;//футы
        public static double Column2EndX = 0;//край второй колонны прям задний край
        public static (double ColomnWidth, double ColomnHeight, double ColomnExc, double ColomnExc2) ColomnSize=(0,0,0,0);

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

            if (!DataSymbols.TryGetValue(symbol, out var data))
            {
                data = new DataSymbol(symbol);
                DataSymbols[symbol] = data;
            }

            //смещения для косоура
            double dx = data.YHeight / 2 * Math.Sin(AngleTg);
            double dz = data.YHeight / 2* Math.Cos(AngleTg);

            double yBorder = -(ColomnSize.ColomnHeight + data.XWidth) / 2 + data.Excentr;

            XYZ startPoint1 = new XYZ(-dx/MMFromFeet, yBorder / MMFromFeet, (dz+GZ)/MMFromFeet);

            double xBorder = -dx + Column2EndX;
            double zBroder = (xBorder - startPoint1.X* MMFromFeet) * Math.Tan(AngleTg);

            XYZ endPoint1 = new XYZ(xBorder / MMFromFeet, yBorder / MMFromFeet, startPoint1.Z+ zBroder / MMFromFeet);



            Line beamCurve1 = Line.CreateBound(startPoint1, endPoint1);
            Element cosour1 = Doc.Create.NewFamilyInstance(beamCurve1, symbol, LevelCreator, StructuralType.Beam);

            ElementTransformUtils.RotateElement(Doc, cosour1.Id, beamCurve1, Math.PI);

            XYZ translation = new XYZ(0, (-StairWidth- data.XWidth )/ MMFromFeet, 0);
            ElementTransformUtils.CopyElement(
            Doc, cosour1.Id, translation);

            ElementTransformUtils.RotateElement(Doc, cosour1.Id, beamCurve1, Math.PI);


        }

    }
}
