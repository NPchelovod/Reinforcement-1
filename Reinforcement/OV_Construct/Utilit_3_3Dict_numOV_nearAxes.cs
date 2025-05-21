using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Documents;
using System.Windows.Media;
using Autodesk.Revit.DB;


/*
 * создаёт словарь - номер группы вентшахт: словарь ближайшие оси А и 1)
 */

namespace Reinforcement
{
    internal class Utilit_3_3Dict_numOV_nearAxes
    {
        public static Dictionary<int, Dictionary<string, string>> Create_Dict_numOV_nearAxes(Document doc, Dictionary<string, Dictionary<string, object>> Dict_Axis, Dictionary<int, Dictionary<string, object>> Dict_Grup_numOV_spisokOV) //ref 
        {
            var Dict_numOV_nearAxes = new Dictionary<int, Dictionary<string, string>>();

            foreach (var iterator in Dict_Grup_numOV_spisokOV)
            {
                int num_grup_OV = iterator.Key;
                var parametrs_OV_grup = iterator.Value;
                var idList = parametrs_OV_grup["spisok_id_ov"] as List<string>;

                var id_ov = new ElementId(Convert.ToInt64(idList[0])); // просто id любой шахты первой среди группы
                Element ventElement = doc.GetElement(id_ov);

                var parametrs_zapis = new Dictionary<string, string>()
                {
                    {"Horizontal_Axe_ID","" },
                    {"Horizontal_Axe_Name","" },
                    {"Vertical_Axe_ID","" },
                    {"Vertical_Axe_Name","" },
                    {"Sosed_Horizontal_Axe_ID","" },
                    {"Sosed_Horizontal_Axe_Name","" },
                    {"Sosed_Vertical_Axe_ID","" },
                    {"Sosed_Vertical_Axe_Name","" },
                    { "Сосед ниже?", "1"},
                    { "Сосед правее?", "1"},

                };

                // координаты центральной точки вентшахты
                LocationPoint ventLocation = ventElement.Location as LocationPoint;
                XYZ ventPoint = ventLocation.Point;


                // теперь идём по всем осям и ищем дистанцию
                // поиск минимальной дистанции по оси X если ось её не параллельна
                double min_dist_x = -1;
                double min_dist_y = -1;
                string id_axe_X = "";
                string id_axe_Y = "";

                string name_axe_X = "";
                string name_axe_Y = "";

                bool naprav_right = true;
                bool naprav_down = true;
                foreach (var iter in Dict_Axis)
                {
                    var tek_id_axe = iter.Key;
                    var parametrs_axe = iter.Value;

                    ElementId axisId = new ElementId(Convert.ToInt64(tek_id_axe));
                    var Element_axe = doc.GetElement(tek_id_axe);
                    Grid grid = Element_axe as Grid;
                    Curve axisCurve = grid.Curve;

                    XYZ projectedPoint = axisCurve.Project(ventPoint).XYZPoint; // точка пересечения с осью проекции

                    double Len_axe_ov = projectedPoint.DistanceTo(ventPoint);


                    bool vert_axe = false;
                    // определение вертикальная ось или горизонтальная

                    XYZ startPoint = axisCurve.GetEndPoint(0);
                    XYZ endPoint = axisCurve.GetEndPoint(1);

                    if (Math.Abs(startPoint.Y - endPoint.Y)-Math.Abs(startPoint.X - endPoint.X)>0)
                    {
                        vert_axe = true; // вертикальнее чем горизонтальнее
                    }

                    if (vert_axe)
                    {
                        // это вертикальная ось
                        
                        if (min_dist_x == -1 || Len_axe_ov < min_dist_x)
                        {
                            id_axe_X = tek_id_axe;
                            name_axe_X = parametrs_axe["name_axe"].ToString();

                            min_dist_x = Len_axe_ov;
                        }

                    }
                    else
                    {
                        // это горизонтальная ось
                        
                        if (min_dist_y == -1 || Len_axe_ov < min_dist_y)
                        {
                            id_axe_Y = tek_id_axe;
                            name_axe_Y = parametrs_axe["name_axe"].ToString();

                            min_dist_y = Len_axe_ov;
                        }
                    }
                   
                }

                parametrs_zapis["Horizontal_Axe_ID"] = id_axe_Y;
                parametrs_zapis["Horizontal_Axe_Name"] = name_axe_Y;

                parametrs_zapis["Vertical_Axe_ID"] = id_axe_X;
                parametrs_zapis["Vertical_Axe_Name"] = name_axe_X;

                var parametrs_axe_hor = Dict_Axis[id_axe_Y];
                var parametrs_axe_vert = Dict_Axis[id_axe_X];

                if (parametrs_axe_hor["down_border"].ToString() != "")
                {
                    parametrs_zapis["Sosed_Horizontal_Axe_ID"] = parametrs_axe_hor["down_border"].ToString();

                    parametrs_zapis["Sosed_Horizontal_Axe_Name"] = Dict_Axis[parametrs_axe_hor["down_border"].ToString()]["name_axe"].ToString();
                }
                else if (parametrs_axe_hor["top_border"].ToString() != "")
                {
                    parametrs_zapis["Sosed_Horizontal_Axe_ID"] = parametrs_axe_hor["top_border"].ToString();

                    parametrs_zapis["Sosed_Horizontal_Axe_Name"] = Dict_Axis[parametrs_axe_hor["top_border"].ToString()]["name_axe"].ToString();

                    naprav_down = false;
                }

                if (parametrs_axe_vert["right_border"].ToString() != "")
                {
                    parametrs_zapis["Sosed_Vertical_Axe_ID"] = parametrs_axe_vert["right_border"].ToString();

                    parametrs_zapis["Sosed_Vertical_Axe_Name"] = Dict_Axis[parametrs_axe_vert["right_border"].ToString()]["name_axe"].ToString();
                }
                else if (parametrs_axe_vert["left_border"].ToString() != "")
                {
                    parametrs_zapis["Sosed_Vertical_Axe_ID"] = parametrs_axe_vert["left_border"].ToString();

                    parametrs_zapis["Sosed_Vertical_Axe_Name"] = Dict_Axis[parametrs_axe_vert["left_border"].ToString()]["name_axe"].ToString();

                    naprav_right = false;
                }

                if (naprav_down == false)
                {
                    parametrs_zapis["Сосед ниже?"] = "0";

                }
                if (naprav_right == false)
                {
                    parametrs_zapis["Сосед правее?"] = "0";
                }


                Dict_numOV_nearAxes[num_grup_OV] = parametrs_zapis;

            }

            return Dict_numOV_nearAxes;

        }

    }
}






