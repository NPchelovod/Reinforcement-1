﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;




/*
 * создаем словарь осей которые точно есть на нашем плане
 */

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_2Dict_Axis
    {

        public static void Dict_Axis( )
        {
            Document doc = RevitAPI.Document;
            var Dict_Axis = new Dictionary<string, Dictionary<string, object>>();
            List<Grid> gridList = null;
            OV_Construct_All_Dictionary.Dict_Axis.Clear();
            using (Transaction trans = new Transaction(doc, "Create Grids"))
            {
                trans.Start();
                foreach (var levelPlan in OV_Construct_All_Dictionary.Dict_level_plan_floor)
                {
                    

                    string currentLevel = levelPlan.Key;
                    //ViewPlan viewPlan = levelPlan.Value;
                    ElementId viewId = levelPlan.Value;

                    var viewPlan = doc.GetElement(viewId) as View;
                    //int viewScale = viewPlan.Scale;
                    //uidoc.ActiveView = viewPlan;
                    
                    //View view = doc.GetElement(viewId) as View;
                    var options = new Options()
                    {
                        ComputeReferences = true,
                        View = viewPlan,
                        IncludeNonVisibleObjects = true
                    };

                    gridList = new FilteredElementCollector(doc, viewPlan.Id)
                    .OfClass(typeof(Grid))
                    .ToElements()
                    .Cast<Grid>()
                    .ToList(); //get all grids on activeView

                    foreach (Grid grid in gridList)
                    {
                        Dict_Axis = Create_Dict_Axis(Dict_Axis, gridList);
                    }

                    
                }

                if (gridList.Count > 0 && Dict_Axis != null && Dict_Axis.Count > 2)
                {
                    OV_Construct_All_Dictionary.Dict_Axis = Dict_Axis; // полная замена
                }
                else
                {
                    List<Grid> gridList2 = new FilteredElementCollector(doc).OfClass(typeof(Grid)).ToElements().Cast<Grid>().ToList(); //get all grids on activeView;
                    Dict_Axis = Create_Dict_Axis(Dict_Axis, gridList2);
                    TaskDialog.Show("Ошибка", "Осей на планах ОВ не было");
                    OV_Construct_All_Dictionary.Dict_Axis = Dict_Axis; // полная замена
                }
                trans.Commit();

            }
        }

        public static Dictionary<string, Dictionary<string, object>> Create_Dict_Axis(Dictionary<string, Dictionary<string, object>> Dict_Axis, List<Grid> axis_Grid,ForgeTypeId units = null) //ref 
        {
            if (units == null)
            {
                units = UnitTypeId.Millimeters; 
            }
            // Создаем StringBuilder для формирования сообщения
            StringBuilder messageBuilder = new StringBuilder();

            foreach (Grid grid in axis_Grid)
            {

                Curve gridCurve = grid.Curve;

                ElementId Id_axe = grid.Id;
                
                if (Dict_Axis.ContainsKey(Id_axe.ToString())) // прямо содержит id
                {
                    continue;
                }

                string name_axe = grid.Name;

                bool exist_axe = false;
                // вдруг такая ось уже была с одинаковым именем, но разным id
                foreach (var iterator in Dict_Axis)
                {
                    if (iterator.Value["name_axe"].ToString() == name_axe.ToString())
                    {
                        messageBuilder.AppendLine($"name {name_axe} id дубляжа: {iterator.Key},{Id_axe.ToString()}");
                        exist_axe = true;
                        break;
                    }
                }
                if (exist_axe)
                {
                    continue;
                }

                // Get start and end points of the grid line
                XYZ startPoint = gridCurve.GetEndPoint(0);
                XYZ endPoint = gridCurve.GetEndPoint(1);

                // конвертация в метры
                double coord_X = UnitUtils.ConvertFromInternalUnits(startPoint.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
                double coord_Y = UnitUtils.ConvertFromInternalUnits(startPoint.Y, units);
                double coord_Z = UnitUtils.ConvertFromInternalUnits(startPoint.Z, units);

                double coord_X2 = UnitUtils.ConvertFromInternalUnits(endPoint.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
                double coord_Y2 = UnitUtils.ConvertFromInternalUnits(endPoint.Y, units);

                // поиск угла наклона оси Y=tan_k*X+B
                double tan_k = 0;
                bool correct_axe = false;
                double strog_vert = 0; // строго вертикальная ось?
                double strog_hor = 0; // строго горизонтальынй?

                if (Math.Abs(coord_Y2 - coord_Y) <= 1)
                {
                    tan_k = 0;
                    strog_hor = 1;
                    if (Math.Abs(coord_Y2 - coord_Y) > 1)
                    {
                        correct_axe = true;
                    }
                }
                else if (Math.Abs(coord_X2 - coord_X) <= 1)
                {
                    tan_k = 0;
                    strog_vert = 1;
                    if (Math.Abs(coord_X2 - coord_X) > 1)
                    {
                        correct_axe = true;
                    }
                }
                else
                {
                    // ось под углом
                    tan_k = (coord_Y2 - coord_Y) / (coord_X2 - coord_X);
                    if (Math.Abs(tan_k) >= 150) // точность пол градуча, то есть 89,5 градусов округл в 90
                    {
                        tan_k = 150;
                        correct_axe = true; // требуется корректировка данной оси неверной
                        strog_vert = 1;

                    }
                    else if (Math.Abs(tan_k) <= 0.015)
                    {
                        tan_k = 0;
                        correct_axe = true;
                        strog_hor = 1;
                    }

                }



                var parametrs_axe = new Dictionary<string, object>()
                {
                    {"name_axe", name_axe },
                    {"coord_X",coord_X},
                    {"coord_Y",coord_Y},
                    {"coord_Z",coord_Z},
                    {"tan_k",tan_k},
                    {"coord_X2",coord_X2},
                    {"coord_Y2",coord_Y2},
                    {"strog_vert", strog_vert},
                    {"strog_hor", strog_hor},
                    {"error_location",correct_axe }, // неточное расположение осей
                    {"left_border","" },
                    {"right_border","" },
                    {"top_border","" },
                    {"down_border","" },
                    {"количество попаданий в план",-1}
                };

                Dict_Axis[Id_axe.ToString()] = parametrs_axe;

            }

            if (messageBuilder.ToString().Length > 0)
            {
                TaskDialog.Show("Дублирование осей с одинаковым именеи", messageBuilder.ToString());
            }
            // теперь надо найти оси соседи - правые и левые, нижние и верхние

            foreach (var iter in Dict_Axis)
            {
                var tek_Id = iter.Key;
                var parametrs_axe = iter.Value;
                var strog_vert = Convert.ToDouble(parametrs_axe["strog_vert"]);
                var strog_hor = Convert.ToDouble(parametrs_axe["strog_hor"]);
                var tan_k = Convert.ToDouble(parametrs_axe["tan_k"]);
                var coord_X = Convert.ToDouble(parametrs_axe["coord_X"]);
                var coord_Y = Convert.ToDouble(parametrs_axe["coord_Y"]);

                if (strog_vert > 0 || tan_k > 1)
                {
                    // значит ищем правую и левую границы
                    if (parametrs_axe["left_border"].ToString() == "")
                    {
                        double min_dist_X = -1;
                        string id_opt_left = "";
                        foreach (var iter2 in Dict_Axis)
                        {
                            var sravn_X = Convert.ToDouble(iter2.Value["coord_X"]);
                            var strog_vert1 = Convert.ToDouble(iter2.Value["strog_vert"]);
                            var tan_k1 = Convert.ToDouble(iter2.Value["tan_k"]);
                            if (strog_vert1 < 1 && tan_k1 < 1 || iter2.Key == tek_Id)
                            {
                                continue; // если не вертикальную ось сравниваем
                            }

                            double razn = coord_X - sravn_X;
                            if (min_dist_X == -1 || razn > 0 && razn < min_dist_X)
                            {
                                min_dist_X = razn;
                                id_opt_left = iter2.Key;
                            }
                        }
                        if (id_opt_left != "")
                        {
                            Dict_Axis[tek_Id]["left_border"] = id_opt_left;
                            // а для нее - это правая граница
                            Dict_Axis[id_opt_left]["right_border"] = tek_Id;
                        }


                    }
                    if (parametrs_axe["right_border"].ToString() == "")
                    {
                        double min_dist_X = -1;
                        string id_opt_right = "";
                        foreach (var iter2 in Dict_Axis)
                        {
                            var sravn_X = Convert.ToDouble(iter2.Value["coord_X"]);
                            var strog_vert1 = Convert.ToDouble(iter2.Value["strog_vert"]);
                            var tan_k1 = Convert.ToDouble(iter2.Value["tan_k"]);
                            if (strog_vert1 < 1 && tan_k1 < 1 || iter2.Key == tek_Id)
                            {
                                continue; // если не вертикальную ось сравниваем
                            }

                            double razn = sravn_X - coord_X;
                            if (min_dist_X == -1 || razn > 0 && razn < min_dist_X)
                            {
                                min_dist_X = razn;
                                id_opt_right = iter2.Key;
                            }
                        }
                        if (id_opt_right != "")
                        {
                            Dict_Axis[tek_Id]["right_border"] = id_opt_right;
                            // а для нее - это правая граница
                            Dict_Axis[id_opt_right]["left_border"] = tek_Id;
                        }


                    }

                }
                else
                {
                    // ищем по Y
                    // значит ищем нижнюю и верхнюю границы
                    if (parametrs_axe["down_border"].ToString() == "")
                    {
                        double min_dist_Y = -1;
                        string id_opt_down = "";
                        foreach (var iter2 in Dict_Axis)
                        {
                            var sravn_Y = Convert.ToDouble(iter2.Value["coord_Y"]);
                            var strog_hor1 = Convert.ToDouble(iter2.Value["strog_hor"]);
                            var tan_k1 = Convert.ToDouble(iter2.Value["tan_k"]);
                            if (strog_hor1 < 1 && tan_k1 >= 1 || iter2.Key == tek_Id)
                            {
                                continue; // если не вертикальную ось сравниваем
                            }

                            double razn = coord_Y - sravn_Y;
                            if (min_dist_Y == -1 || razn > 0 && razn < min_dist_Y)
                            {
                                min_dist_Y = razn;
                                id_opt_down = iter2.Key;
                            }
                        }
                        if (id_opt_down != "")
                        {
                            Dict_Axis[tek_Id]["down_border"] = id_opt_down;
                            // а для нее - это правая граница
                            Dict_Axis[id_opt_down]["top_border"] = tek_Id;
                        }


                    }
                    if (parametrs_axe["top_border"].ToString() == "")
                    {
                        double min_dist_Y = -1;
                        string id_opt_top = "";
                        foreach (var iter2 in Dict_Axis)
                        {
                            var sravn_Y = Convert.ToDouble(iter2.Value["coord_Y"]);
                            var strog_hor1 = Convert.ToDouble(iter2.Value["strog_hor"]);
                            var tan_k1 = Convert.ToDouble(iter2.Value["tan_k"]);
                            if (strog_hor1 < 1 && tan_k1 >= 1 || iter2.Key == tek_Id)
                            {
                                continue; // если не вертикальную ось сравниваем
                            }

                            double razn = sravn_Y - coord_Y;
                            if (min_dist_Y == -1 || razn > 0 && razn < min_dist_Y)
                            {
                                min_dist_Y = razn;
                                id_opt_top = iter2.Key;
                            }
                        }
                        if (id_opt_top != "")
                        {
                            Dict_Axis[tek_Id]["top_border"] = id_opt_top;
                            // а для нее - это правая граница
                            Dict_Axis[id_opt_top]["down_border"] = tek_Id;
                        }

                    }
                }
            }
            return Dict_Axis;
        }
    }
}
