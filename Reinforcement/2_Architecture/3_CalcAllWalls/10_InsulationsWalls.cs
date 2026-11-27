using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement
{
    public static class InsulationsWalls
    {
        
        /// <summary>
        /// Признак что стена наружная
        /// </summary>
        private static HashSet<string> NameInsulationWalls = new HashSet<string>() { "Наруж", "фасад" };
        /// <summary>
        /// Признак что стена внутренняя
        /// </summary>
        private static HashSet<string> NameInWalls = new HashSet<string>() { "Внутрен" };
        public static bool InsulationNameWall(ECWall wall)
        {
           
            string nameWall = wall.WallType.Name;
            if(string.IsNullOrEmpty(nameWall) ) {return false;}

            foreach (var name in NameInsulationWalls)
            {
                if(nameWall.Contains(name))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Значит стена наверняка не наружная - увеличивает вероятность
        /// </summary>
        /// <param name="wall"></param>
        /// <returns></returns>
        public static bool InNameWall(ECWall wall)
        {

            string nameWall = wall.WallType.Name;
            if (string.IsNullOrEmpty(nameWall)) { return false; }

            foreach (var name in NameInWalls)
            {
                if (nameWall.Contains(name))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
