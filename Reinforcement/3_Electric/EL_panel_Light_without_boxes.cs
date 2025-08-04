using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class EL_panel_Light_without_boxes : IExternalCommand
    {
        //словарь команда и что мы создаем
        // имя команды: (класс, тип класса, требует ли привязки к поверхности )
        public static Dictionary<string, (string FamilyName, string SymbolName, bool RequiresHost)> elementCategories { get; set; } = new Dictionary<string, (string FamilyName, string SymbolName, bool RequiresHost)>()
        {
                { "в перекрытии патроны", ("патрон", "патрон", false) },
                { "в перекрытии клеммы", ("Клеммник_для_распаячной_и_универальной_коробок_шаг_крепления_60_90_EKF_PROxima", "Клеммник для распаячной и универальной коробок, шаг крепления 60", true) },
               // { "в стенах патроны", ("патрон", "патрон", true) },
                //{ "в стенах клеммы", ("Клеммник_для_распаячной", "Клеммник для распаячной и универсальной коробок, шаг крепления 60", true) }
        };




        //словарь что мы заменяем, на какую команду заменяем данные кубики (имя семейства, тип)
        public static Dictionary<string, (string FamilyName, string SymbolName)> replace_cubics { get; set; } = new Dictionary<string, (string FamilyName, string SymbolName)>()
        {
            { "в перекрытии патроны",("Коробка ЭЛ_КУ1301","КУ1301_патрон") },
            { "в перекрытии клеммы",("Коробка ЭЛ_КУ1301","КУ1301_клеммник")}
        };


        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            bool r= EL_panel_step0_allCommand.CopyTask(commandData, ref message, elements, elementCategories, replace_cubics);


            if (r)
            { return Result.Succeeded; }
            else
                { return Result.Failed; }
        }
    }
}