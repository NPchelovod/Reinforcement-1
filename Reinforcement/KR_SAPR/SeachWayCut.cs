using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    // Поиск кратчайшего маршрута как в Яндекс яндекс картах

    public interface CoordData
    {
        double X { get; set; }
        double Y { get; set; }

        int NumWay { get; set; } // число которое показывает порядок, 1 - первые элементы значит идут до вторых и тд

    }
    public static class OpenTspSolver
    {
        /// <summary>
        /// Основной метод: строит незамкнутый маршрут, проходящий все точки в порядке NumWay
        /// (сначала все с NumWay=1, затем NumWay=2 и т.д.), внутри каждой группы минимизируя длину.
        /// </summary>
        /// <param name="points">Исходный список точек</param>
        /// <param name="timeLimit">Общее ограничение по времени на всю задачу</param>
        /// <returns>Упорядоченный список точек для обхода</returns>
        public static List<CoordData> Solve(List<CoordData> points, TimeSpan timeLimit)
        {
            if (points == null || points.Count == 0)
                return new List<CoordData>();

            var stopwatch = Stopwatch.StartNew();

            // Группируем по NumWay, сортируем группы по возрастанию номера
            var groups = points
                .GroupBy(p => p.NumWay)
                .OrderBy(g => g.Key)
                .Select(g => g.ToList())
                .ToList();

            // Если группа одна — просто решаем открытый TSP для неё
            if (groups.Count == 1)
                return SolveSingleGroup(groups[0], timeLimit);

            // Распределяем время между группами пропорционально количеству точек
            int totalPoints = points.Count;
            var solutions = new List<List<CoordData>>();

            foreach (var group in groups)
            {
                if (group.Count == 0) continue;

                double fraction = (double)group.Count / totalPoints;
                var remaining = timeLimit - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    remaining = TimeSpan.FromMilliseconds(10);

                var groupTimeLimit = TimeSpan.FromMilliseconds(remaining.TotalMilliseconds * fraction);
                solutions.Add(SolveSingleGroup(group, groupTimeLimit));
            }

            // Склеиваем цепочки с оптимизацией ориентации стыков
            return ConcatenateGroups(solutions);
        }

        /// <summary>
        /// Решает открытый TSP для одной группы точек (без учёта NumWay).
        /// </summary>
        private static List<CoordData> SolveSingleGroup(List<CoordData> group, TimeSpan timeLimit)
        {
            if (group.Count <= 2)
                return new List<CoordData>(group);

            var stopwatch = Stopwatch.StartNew();
            var rnd = new Random();
            int n = group.Count;
            var bestTour = new List<CoordData>();
            double bestLength = double.MaxValue;

            // 1. Быстрый мультистарт Nearest Neighbor из нескольких случайных начальных точек
            int nnAttempts = Math.Min(20, n);
            for (int attempt = 0; attempt < nnAttempts; attempt++)
            {
                int startIdx = attempt == 0 ? 0 : rnd.Next(n);
                var tour = NearestNeighborTour(group, startIdx);
                double len = TourLength(tour);
                if (len < bestLength)
                {
                    bestLength = len;
                    bestTour = new List<CoordData>(tour);
                }
            }

            // 2. Итеративный локальный поиск (2-opt + double-bridge)
            int maxIdle = 50;
            int idle = 0;
            var currentTour = new List<CoordData>(bestTour);
            double currentLength = bestLength;

            while (stopwatch.Elapsed < timeLimit && idle < maxIdle)
            {
                bool improved;
                do
                {
                    improved = TwoOpt(currentTour);
                    currentLength = TourLength(currentTour);
                } while (improved);

                if (currentLength < bestLength - 1e-12)
                {
                    bestLength = currentLength;
                    bestTour = new List<CoordData>(currentTour);
                    idle = 0;
                }
                else
                {
                    idle++;
                }

                if (stopwatch.Elapsed < timeLimit)
                {
                    DoubleBridgePerturbation(currentTour, rnd);
                    currentLength = TourLength(currentTour);
                }
            }

            return bestTour;
        }

        /// <summary>
        /// Склеивает цепочки групп, при необходимости разворачивая их для минимизации расстояния в стыках.
        /// </summary>
        private static List<CoordData> ConcatenateGroups(List<List<CoordData>> groupPaths)
        {
            if (groupPaths.Count == 0) return new List<CoordData>();

            var result = new List<CoordData>(groupPaths[0]);

            for (int i = 1; i < groupPaths.Count; i++)
            {
                var lastOfPrev = result[result.Count - 1];
                var current = groupPaths[i];
                if (current.Count == 0) continue;

                double distStraight = Dist(lastOfPrev, current[0]);
                double distReversed = Dist(lastOfPrev, current[current.Count - 1]);

                if (distReversed < distStraight)
                {
                    current.Reverse();
                }

                result.AddRange(current);
            }

            return result;
        }

        #region Внутренние методы оптимизации

        private static List<CoordData> NearestNeighborTour(List<CoordData> points, int startIdx)
        {
            int n = points.Count;
            var visited = new bool[n];
            var tour = new List<CoordData>(n);
            int current = startIdx;

            for (int i = 0; i < n; i++)
            {
                visited[current] = true;
                tour.Add(points[current]);

                int next = -1;
                double minDist = double.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (!visited[j])
                    {
                        double d = Dist(points[current], points[j]);
                        if (d < minDist)
                        {
                            minDist = d;
                            next = j;
                        }
                    }
                }

                if (next == -1) break;
                current = next;
            }

            return tour;
        }

        private static bool TwoOpt(List<CoordData> tour)
        {
            int n = tour.Count;
            bool improved = false;

            for (int i = 0; i < n - 2; i++)
            {
                for (int j = i + 2; j < n - 1; j++)
                {
                    double oldLen = Dist(tour[i], tour[i + 1]) + Dist(tour[j], tour[j + 1]);
                    double newLen = Dist(tour[i], tour[j]) + Dist(tour[i + 1], tour[j + 1]);

                    if (newLen < oldLen - 1e-12)
                    {
                        // Разворот участка [i+1 .. j]
                        tour.Reverse(i + 1, j - i);
                        improved = true;
                    }
                }
            }

            return improved;
        }

        private static void DoubleBridgePerturbation(List<CoordData> tour, Random rnd)
        {
            int n = tour.Count;
            if (n < 8) return;

            int p1 = rnd.Next(1, n - 6);
            int p2 = rnd.Next(p1 + 2, n - 4);
            int p3 = rnd.Next(p2 + 2, n - 2);

            var segA = tour.GetRange(0, p1 + 1);
            var segB = tour.GetRange(p1 + 1, p2 - p1);
            var segC = tour.GetRange(p2 + 1, p3 - p2);
            var segD = tour.GetRange(p3 + 1, n - p3 - 1);

            var newTour = new List<CoordData>(n);
            newTour.AddRange(segA);
            newTour.AddRange(segD);
            newTour.AddRange(segC);
            newTour.AddRange(segB);

            tour.Clear();
            tour.AddRange(newTour);
        }

        private static double Dist(CoordData a, CoordData b)
        {
            //Замена Math.Sqrt(dx*dx + dy*dy) на просто (dx*dx + dy*dy) нарушит логику алгоритма, потому
            //Если заменить d на d², то порядок величин изменится нелинейно.
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double TourLength(List<CoordData> tour)
        {
            double len = 0;
            for (int i = 1; i < tour.Count; i++)
                len += Dist(tour[i - 1], tour[i]);
            return len;
        }

        #endregion
    }

    public static class OpenTspSolve2r
    {
        // Открытый путь: точки в порядке обхода (первая и последняя — концы)
        public static List<CoordData> Solve(List<CoordData> points, TimeSpan timeLimit)
        {
            var stopwatch = Stopwatch.StartNew();
            var rnd = new Random(); // <-- обычный Random
            int n = points.Count;
            var bestTour = new List<CoordData>();
            double bestLength = double.MaxValue;

            // 1. Мультистарт Nearest Neighbor
            int nnAttempts = Math.Min(20, n);
            for (int attempt = 0; attempt < nnAttempts; attempt++)
            {
                int startIdx = attempt == 0 ? 0 : rnd.Next(n);
                var tour = NearestNeighborTour(points, startIdx);
                double len = TourLength(tour);
                if (len < bestLength)
                {
                    bestLength = len;
                    bestTour = new List<CoordData>(tour);
                }
            }

            // 2. Итеративный локальный поиск
            int maxIdle = 50;
            int idle = 0;
            var currentTour = new List<CoordData>(bestTour);
            double currentLength = bestLength;

            while (stopwatch.Elapsed < timeLimit && idle < maxIdle)
            {
                bool improved;
                do
                {
                    improved = TwoOpt(currentTour);
                    currentLength = TourLength(currentTour);
                } while (improved);

                if (currentLength < bestLength)
                {
                    bestLength = currentLength;
                    bestTour = new List<CoordData>(currentTour);
                    idle = 0;
                }
                else
                {
                    idle++;
                }

                if (stopwatch.Elapsed < timeLimit)
                {
                    DoubleBridgePerturbation(currentTour, rnd); // передаём rnd
                    currentLength = TourLength(currentTour);
                }
            }

            return bestTour;
        }
        static void DoubleBridgePerturbation(List<CoordData> tour, Random rnd)
        {
            int n = tour.Count;
            if (n < 8) return;
            int p1 = rnd.Next(1, n - 6);
            int p2 = rnd.Next(p1 + 2, n - 4);
            int p3 = rnd.Next(p2 + 2, n - 2);

            var segmentA = tour.GetRange(0, p1 + 1);
            var segmentB = tour.GetRange(p1 + 1, p2 - p1);
            var segmentC = tour.GetRange(p2 + 1, p3 - p2);
            var segmentD = tour.GetRange(p3 + 1, n - p3 - 1);

            var newTour = new List<CoordData>(n);
            newTour.AddRange(segmentA);
            newTour.AddRange(segmentD);
            newTour.AddRange(segmentC);
            newTour.AddRange(segmentB);

            tour.Clear();
            tour.AddRange(newTour);
        }
        // Жадное построение открытого пути из заданной стартовой точки
        static List<CoordData> NearestNeighborTour(List<CoordData> points, int startIdx)
        {
            int n = points.Count;
            var visited = new bool[n];
            var tour = new List<CoordData>(n);
            int current = startIdx;
            for (int i = 0; i < n; i++)
            {
                visited[current] = true;
                tour.Add(points[current]);
                int next = -1;
                double minDist = double.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (!visited[j])
                    {
                        double d = Dist(points[current], points[j]);
                        if (d < minDist)
                        {
                            minDist = d;
                            next = j;
                        }
                    }
                }
                if (next == -1) break;
                current = next;
            }
            return tour;
        }

        // 2‑opt для открытого пути (возвращает true, если сделано улучшение)
        static bool TwoOpt(List<CoordData> tour)
        {
            int n = tour.Count;
            bool improved = false;
            // Для открытого пути индексы i..(i+1) и j..(j+1), j >= i+2
            for (int i = 0; i < n - 2; i++)
            {
                for (int j = i + 2; j < n - 1; j++)
                {
                    double oldLen = Dist(tour[i], tour[i + 1]) + Dist(tour[j], tour[j + 1]);
                    double newLen = Dist(tour[i], tour[j]) + Dist(tour[i + 1], tour[j + 1]);
                    if (newLen < oldLen - 1e-12)
                    {
                        // Переворот участка с i+1 по j (включительно)
                        tour.Reverse(i + 1, j - i); // Length = j - i
                        improved = true;
                    }
                }
            }
            return improved;
        }

        // Double‑bridge perturbation: разбиваем маршрут на 4 части и переставляем их
        //static void DoubleBridgePerturbation(List<CoordData> tour)
        //{
        //    int n = tour.Count;
        //    if (n < 8) return;
        //    // Выбираем случайные точки разреза, соблюдая порядок
        //    int p1 = Random.Shared.Next(1, n - 6);
        //    int p2 = Random.Shared.Next(p1 + 2, n - 4);
        //    int p3 = Random.Shared.Next(p2 + 2, n - 2);
        //    // Участки: A [0..p1], B [p1+1..p2], C [p2+1..p3], D [p3+1..n-1]
        //    var segmentA = tour.GetRange(0, p1 + 1);
        //    var segmentB = tour.GetRange(p1 + 1, p2 - p1);
        //    var segmentC = tour.GetRange(p2 + 1, p3 - p2);
        //    var segmentD = tour.GetRange(p3 + 1, n - p3 - 1);

        //    // Новая последовательность: A + D + C + B (одна из возможных перестановок)
        //    var newTour = new List<CoordData>(n);
        //    newTour.AddRange(segmentA);
        //    newTour.AddRange(segmentD);
        //    newTour.AddRange(segmentC);
        //    newTour.AddRange(segmentB);

        //    // Заменяем текущий тур
        //    tour.Clear();
        //    tour.AddRange(newTour);
        //}

        static double Dist(CoordData a, CoordData b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        static double TourLength(List<CoordData> tour)
        {
            double len = 0;
            for (int i = 1; i < tour.Count; i++)
                len += Dist(tour[i - 1], tour[i]);
            return len;
        }
    }
}
