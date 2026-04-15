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
    public static partial class StairCreator
    {
        public static double ColumnTopZ = 0;//футы
        public static double Column2EndX = 0;//край второй колонны прям задний край

        public static double CenterAllLk => Column2EndX/2;// центр по X всей лестницы

        public static DataSymbol Column;
        public static DataSymbol Cosour;
        public static void CreateOpors()
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
            }
            ColumnTopZ = NumStair * StairHeight;

            //в миллиметрах
            //на виде сбоку это ширина сечения и высота
            //на плане это размер по Y, затем по X




            //// Revit работает во внутренних футах. Вводимые пользователем миллиметры нужно преобразовывать: double feet = mm / 304.8;.
            XYZ startPoint1 = new XYZ(Column.W / 2 / MMFromFeet, 0, GZ / MMFromFeet);
            XYZ endPoint1 = new XYZ(Column.W / 2 / MMFromFeet, 0, ColumnTopZ / MMFromFeet);
            //CreateElement(symbol, LevelCreator, new XYZ(0, 0, 0), new XYZ(0, 0, 2));
            //CreateElement(symbol, LevelCreator, new XYZ(0, 0, 0), new XYZ(0, 0, 2), Math.PI / 2);

            Element column1 = CreateElement(symbol, LevelCreator, startPoint1, endPoint1, Math.PI / 2);


            Column2EndX = (StupenL + StairLength);//задняя часть колонны
            double column2CenterX = Column2EndX - Column.W / 2;

            XYZ startPoint2 = new XYZ(column2CenterX / MMFromFeet, 0, GZ / MMFromFeet);
            XYZ endPoint2 = new XYZ(column2CenterX / MMFromFeet, 0, ColumnTopZ / MMFromFeet);

            CreateElement(symbol, LevelCreator, startPoint2, endPoint2, Math.PI / 2);
            //CreateElement(symbol, LevelCreator, startPoint2, endPoint2, Math.PI / 2);

            CreateCosours();
        }


        
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
            double dx = Cosour.H / 2 * Math.Sin(AngleTg);
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
            //XYZ startPoint = new XYZ(startPoint1.X, 0, startPoint1.Z);
            //XYZ endPoint = new XYZ(endPoint1.X, 0, endPoint1.Z);
            //Element cosour = CreateElement(symbol, LevelCreator, startPoint, endPoint);
            //Element cosour22 = CreateElement(symbol, LevelCreator, startPoint, endPoint);
            //cosour22 = RotateElement(cosour22, Math.PI);


            XYZ translation = new XYZ(0, (-StairWidth - Cosour.W) / MMFromFeet, 0);
            Element cosour2 = CreateMoveElement(cosour1, translation, 0);
            cosour2 = RotateElement(cosour2, Math.PI);


            //создаём элементы площадки


            double xPlos1 = -WidthPloshadka;
            double xPlos2 = Column2EndX + WidthPloshadka;
            //строим косоуры площадки
            XYZ startPointPlos11 = new XYZ(xPlos1 / MMFromFeet, startPoint1.Y, startPoint1.Z);
            Line beamCurvePlos11 = Line.CreateBound(startPointPlos11, startPoint1);

            XYZ startPointPlos21 = new XYZ(xPlos2 / MMFromFeet, endPoint1.Y, endPoint1.Z);
            Line beamCurvePlos21 = Line.CreateBound(endPoint1, startPointPlos21);

            Element cosourPlos11 = CreateElement(symbol, LevelCreator, startPointPlos11, startPoint1);
            cosourPlos11 = RotateElement(cosourPlos11, Math.PI);

            Element cosourPlos21 = CreateElement(symbol, LevelCreator, endPoint1, startPointPlos21);
            cosourPlos11 = RotateElement(cosourPlos21, Math.PI);
            Doc.Regenerate();
            //и отражаем их
            Element cosourPlos12 = CreateMoveElement(cosourPlos11, translation, 0);
            cosourPlos12 = RotateElement(cosourPlos12, Math.PI);
            Element cosourPlos22 = CreateMoveElement(cosourPlos21, translation, 0);
            cosourPlos22 = RotateElement(cosourPlos22, Math.PI);

            //ElementTransformUtils.RotateElement(Doc, cosour1.Id, beamCurve1, Math.PI);

            //XYZ translation = new XYZ(0, (-StairWidth - Cosour.W) / MMFromFeet, 0);




            //Element cosour2 = CreateMoveElement(cosour1, translation, Math.PI);



            //ElementTransformUtils.CopyElement(
            //Doc, cosour1.Id, translation);

            //ElementTransformUtils.RotateElement(Doc, cosour1.Id, beamCurve1, Math.PI);

        }


    }
}
