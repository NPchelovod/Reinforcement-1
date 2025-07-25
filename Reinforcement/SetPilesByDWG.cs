﻿using Autodesk.Revit.Attributes;
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

    public class SetPilesByDWG : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            FilteredElementCollector collection = new FilteredElementCollector(doc);

            string familyName = "ЕС_Буронабивная свая";

            //Проверка есть ли в проекте "ЕС_Буронабивная свая"
            bool famExist = collection
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Any(family => family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));

            if (!famExist)
            {
                MessageBox.Show($"Не найдено семейство {familyName}!");
                return Result.Failed;
            }


            //Типоразмер нужной сваи
            var pileId = collection.OfClass(typeof(Family))
                .Where(x => x.Name == familyName)
                .Cast<Family>()
                .FirstOrDefault()
                .GetFamilySymbolIds()
                .FirstOrDefault();


            var pile = doc.GetElement(pileId) as FamilySymbol;

            //Самый нижний уровень
            var check = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToElements();

            var level = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .FirstOrDefault();

            TransparentNotificationWindow.ShowNotification("Выберите подложку dwg", uidoc, 5);

            Reference sel = uidoc.Selection.PickObject(ObjectType.Element);          
            var dwg = doc.GetElement(sel);
            if (!(dwg is ImportInstance))
            {
                MessageBox.Show("Выбрана не подложка!\n" + "Категория должна быть ImportInstance");
                return Result.Failed;
            }

            Options opt = new Options()
            {
                ComputeReferences = true,
                View = doc.ActiveView
            };

            var geom = dwg.get_Geometry(opt).First() as GeometryInstance;
            var geomList = geom.GetInstanceGeometry()
                .OfType<Point>()
                .Select(x => x.Coord)                
                .ToList();

            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    if (pile != null && !pile.IsActive)
                    {
                        pile.Activate();
                        doc.Regenerate();
                    }
                    foreach (XYZ point in geomList) 
                    {
                        doc.Create.NewFamilyInstance(point, pile, level, Autodesk.Revit.DB.Structure.StructuralType.Footing);
                    }
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                //Код в случае ошибки
                MessageBox.Show("Чет пошло не так!\n" + ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }
    }
}
