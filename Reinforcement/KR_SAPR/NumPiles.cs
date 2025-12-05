using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class NumPiles : IExternalCommand
    {

        //имена типоразмеров семейства

        private HashSet<string> Piles = new HashSet<string>()
        {
            //"ЕС_Буронабивная свая",  "ЕС_Буронабивная Свая"
            "ADSK_Свая_", "ЕС_Буронабивная, ЕС_Свая", "Свая", "свая"
        };

        private double sectorStep = 1400; // шаг поиска свай
        private double sectorStepZ = 100; // шаг разбивки УГО по высоте

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            var Seacher = HelperSeachAllElements.SeachAllElements(Piles, commandData, true);

            if (Seacher.Count == 0 || sectorStep < 1)
            { return Result.Failed; }

            ForgeTypeId units = UnitTypeId.Millimeters;
            //var DictPiles = new Dictionary<(int Xs, int Ys), D<Element>>();

            var PropertiesPiles = new Dictionary<Element, (int Xs, int Ys, int Zs, double x, double y, double z, string Name, int numPile, int numGrup)>();
            var DictSector = new Dictionary<(int Xs, int Ys), HashSet<Element>>();
            foreach (Element pile in Seacher)
            {
                // получаем координаты
                LocationPoint tek_locate = pile.Location as LocationPoint; // текущая локация вентканала
                XYZ tek_locate_point = tek_locate.Point; // текущая координата расположения

                double coord_X = UnitUtils.ConvertFromInternalUnits(tek_locate_point.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
                double coord_Y = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Y, units);
                double coord_Z = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Z, units);

                //определение сектора
                int Xs = (int)Math.Round(coord_X / sectorStep);
                int Ys = (int)Math.Round(coord_Y / sectorStep);
                int Zs = (int)Math.Round(coord_Z / sectorStepZ);
                string name = pile.Name;
                PropertiesPiles[pile] = (Xs, Ys, Zs, coord_X, coord_Y, coord_Z, name,-1,-1);
                var sector = (Xs, Ys);
                if(DictSector.ContainsKey(sector))
                {
                    DictSector[sector].Add(pile);
                }
                else
                {
                    DictSector[sector] = new HashSet<Element> { pile };
                }
            }

            //теперь ищем родственные сваи аналог графа
            var listInt = new List<int> { -1, 1 };
            int numGrup = 0;

            
            var listSectors = DictSector.Keys.ToList();
            for (int s=0;s< listSectors.Count;s++)
            {
                var sector = listSectors[s];
                int Xs = sector.Xs;
                int Ys= sector.Ys;

                var anyPile= DictSector[sector].FirstOrDefault();

                int nextNumGrup = PropertiesPiles[anyPile].numGrup; // 
                
                foreach (int i in listInt)
                {
                    foreach (int j in listInt)
                    {
                        var sector2 = (Xs + i,Ys+i);
                        if(DictSector.TryGetValue(sector2,out var hashChilds))
                        {
                            var anyPile2 = DictSector[sector2].FirstOrDefault();
                            int nextNumGrup2 = PropertiesPiles[anyPile2].numGrup; // 
                            if (nextNumGrup>0 && )
                        }
                    }
                }


            }



                return Result.Succeeded;


        }
        


    }
}
