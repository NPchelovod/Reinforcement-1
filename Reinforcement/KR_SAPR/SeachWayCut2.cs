using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

namespace Reinforcement.KR_SAPR
{

    //Предложенный вами алгоритм решает задачу коммивояжёра (TSP) с группировкой по NumWay. Для поиска кратчайшего пути от конкретной точки A до точки B с учётом ограничений BorderWays и AllowedPaths достаточно использовать алгоритм Дейкстры на ориентированном графе, построенном по следующим правилам:
    public static class ShortestPathFinder
    {
        /// <summary>
        /// Находит кратчайший путь от start до end среди всех точек.
        /// Если BorderWays == true, переход разрешён только в точки из AllowedPaths.
        /// </summary>
        /// <param name="points">Все доступные точки (включая start и end)</param>
        /// <param name="start">Начальная точка</param>
        /// <param name="end">Конечная точка</param>
        /// <param name="timeLimit">Ограничение по времени (опционально)</param>
        /// <returns>Список точек от start до end включительно; пустой список, если пути нет</returns>
        public static List<CoordData> FindPath(List<CoordData> points, CoordData start, CoordData end,
                                               TimeSpan timeLimit = default)
        {
            if (points == null || start == null || end == null)
                return new List<CoordData>();

            // Если лимит не задан, ставим заведомо большой
            if (timeLimit == default)
                timeLimit = TimeSpan.FromSeconds(10);

            var sw = Stopwatch.StartNew();

            // Словари для алгоритма Дейкстры
            var dist = new Dictionary<CoordData, double>();
            var prev = new Dictionary<CoordData, CoordData>();
            var unvisited = new HashSet<CoordData>();

            foreach (var p in points)
            {
                dist[p] = double.PositiveInfinity;
                prev[p] = null;
                unvisited.Add(p);
            }

            if (!dist.ContainsKey(start) || !dist.ContainsKey(end))
                return new List<CoordData>();

            dist[start] = 0.0;

            while (unvisited.Count > 0 && sw.Elapsed < timeLimit)
            {
                // Выбор непосещённой вершины с минимальным расстоянием
                CoordData current = null;
                double bestDist = double.PositiveInfinity;
                foreach (var node in unvisited)
                {
                    if (dist[node] < bestDist)
                    {
                        bestDist = dist[node];
                        current = node;
                    }
                }

                // Если оставшиеся вершины недостижимы или достигнута цель
                if (current == null || double.IsPositiveInfinity(bestDist))
                    break;
                if (current == end)
                    break;

                unvisited.Remove(current);

                // Релаксация рёбер
                foreach (var neighbor in points)
                {
                    if (neighbor == current) continue;
                    if (!unvisited.Contains(neighbor)) continue;

                    // Проверяем возможность перехода
                    bool canMove = !current.BorderWays ||
                                   (current.AllowedPaths != null && current.AllowedPaths.Contains(neighbor));
                    if (!canMove) continue;

                    double alt = dist[current] + Dist(current, neighbor);
                    if (alt < dist[neighbor])
                    {
                        dist[neighbor] = alt;
                        prev[neighbor] = current;
                    }
                }
            }

            // Восстановление пути
            var path = new List<CoordData>();
            if (double.IsPositiveInfinity(dist[end]))
                return path; // путь не найден

            for (var at = end; at != null; at = prev[at])
                path.Add(at);

            path.Reverse();
            return path;
        }

        // Евклидово расстояние (аналогично исходному алгоритму)
        private static double Dist(CoordData a, CoordData b)
        {
            
            return a.Dist(b);
        }
    }
}
