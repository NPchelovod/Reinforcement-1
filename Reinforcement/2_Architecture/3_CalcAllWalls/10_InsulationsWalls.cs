using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement
{
    public class InsulationsWalls
    {


        private static HashSet<string> NameInsulationWalls = new HashSet<string>() { "Наруж", "фасад" };
        public static bool InsulationNameWall(ECWall wall)
        {
            string nameWall = wall.WallType.Name;
            if(string.IsNullOrEmpty(nameWall) ) {return false;}

            foreach (var name in NameInsulationWalls)
            {
                if(nameWall.Contains(name))
                {
                    wall.ECInsulation.OutWallFromNAmeMaterial = true;
                    return true;
                }
            }
            return false;
        }
    }
}
