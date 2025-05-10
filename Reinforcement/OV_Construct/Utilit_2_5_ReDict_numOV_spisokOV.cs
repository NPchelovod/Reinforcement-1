using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Autodesk.Revit.DB;


/*
 * Перезапись словаря по порядку в котором будут пронумерованы шахты на плане этажа
 */

namespace Reinforcement
{
    internal class Utilit_2_5_ReDict_numOV_spisokOV
    {
        public static Dictionary<int, Dictionary<string, object>> ReCreate_Dict_Grup_numOV_spisokOV(Dictionary<int, int> Dict_numerateOV, Dictionary<int, Dictionary<string, object>> Dict_Grup_numOV_spisokOV) //ref 

        {
            var Re_Dict_Grup_numOV_spisokOV = new Dictionary<int, Dictionary<string, object>>();

            foreach (var iter in Dict_numerateOV)
            { 
                int nado_num = iter.Key;
                int tek_num = iter.Value;

                Re_Dict_Grup_numOV_spisokOV[nado_num] = Dict_Grup_numOV_spisokOV[tek_num];

            }
             return Re_Dict_Grup_numOV_spisokOV;

        }

    }
}






