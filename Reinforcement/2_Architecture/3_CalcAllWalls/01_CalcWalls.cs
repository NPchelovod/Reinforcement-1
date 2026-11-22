using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using static Autodesk.Revit.DB.SpecTypeId;

namespace Reinforcement
{
   

    [Transaction(TransactionMode.Manual)]
    public  class CalcWalls : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Succeeded;
            Document doc = uidoc.Document;
            //сбор всех стен

            var lw = GetWalls(doc);//все стены
            

            //затем мои методы подсчета, мой алгоритм
            return Result.Succeeded;
        }
        private static BuiltInCategory OSTWall = BuiltInCategory.OST_Walls;
        private static void ClearCash()
        {
            DictECWalls.Clear();
            DictECLevels.Clear();
            DictWallType.Clear();
            DictECRooms.Clear();
        }
        public static List<ECWall> GetWalls(Document doc)
        {
            //или из выделенных элементов или из вида собираем все стены и данные о них
            ClearCash();
            UIDocument uidoc = RevitAPI.UiDocument;
            
            View view = doc.ActiveView;
            if (view == null) return null;
            int viewIdValue = (int)view.Id.Value;
            if (viewIdValue <= 0) return null;
           
            //нулевая вызывает ошибку
            var selectedIds = uidoc.Selection.GetElementIds();

            var walls = new FilteredElementCollector(doc, selectedIds)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();
            if (walls.Count==0)
            {
                walls = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();
            }
            if (walls.Count == 0)
            {
                return null;
            }

            
            //теперь мы анализируем все элементы
            var answer = new List<ECWall>();
            foreach (var wall in walls)
            {
                Element element = (Element)wall;          // явное приведение
                
                if (element == null) continue;
                //OST получаем
                BuiltInCategory bic = BuiltInCategory.INVALID;
                if (element.Category != null)
                {
                    bic = element.Category.BuiltInCategory;
                }
                if(bic!= OSTWall) continue;
                var w = GetECWall(element, doc);
                if (w != null)
                {
                    answer.Add(w);
                }
            }
            return answer;
        }
        private static Dictionary<long, ECWall> DictECWalls = new Dictionary<long, ECWall>();

        private static ECWall GetECWall(Element element, Document doc)
        {
            Wall wall = element as Wall;
            if (wall == null) return null;
            long id = (int)element.Id.Value;
            if (DictECWalls.TryGetValue(id, out ECWall eCWall))
            {
                return eCWall;
            }
            //тут заполняем все данные 
            eCWall = new ECWall();
            eCWall.Element = element;
            eCWall.ElementId = element.Id;
            eCWall.Id = id;


            DictECWalls[eCWall.Id] = eCWall;

            //Затем заполняем уровень стены
            ReadLevel(eCWall, element, doc);

            //затем внутреннии характеристики стены
            
            ReadWallType(eCWall, wall, element, doc);


            ReadWall(eCWall, wall, element, doc);

            //дверные проемы в стене и другое
            ReadDoorAndWindow(eCWall, wall, element, doc);


            //другие свойства
            ReadWallOtherData(eCWall, wall, element, doc);
            return eCWall;
        }

        private static void ReadLevel(ECWall eCWall, Element element, Document doc)
        {
            ElementId levelId = element.LevelId;
            if (levelId==null|| levelId == ElementId.InvalidElementId)
                return;

            long levelIdInt = levelId.Value;
            if(DictECLevels.TryGetValue(levelIdInt, out ECLevel eCLevel))
            {
                eCWall.ECLevel = eCLevel;
                return;
            }

            Level level = doc.GetElement(levelId) as Level;
            if (level == null)
                return;
            
            //заполняем в словари и в ответ
            eCLevel = new ECLevel();
            DictECLevels[levelIdInt]= eCLevel;

            eCWall.ECLevel = eCLevel;

            //заполняем параметры уровня
            eCLevel.Id = levelIdInt;
            eCLevel.Element = level;
            eCLevel.Level = level;

            eCLevel.Name = level.Name;                 // <-- имя уровня
            eCLevel.LevelElevation = level.ProjectElevation; // отметка уровня от проектного нуля 
        }



        private static Dictionary<long, ECLevel> DictECLevels = new Dictionary<long, ECLevel>();

        /// <summary>
        /// Параметры типоразмера стены
        /// </summary>
        /// <param name="eCWall"></param>
        /// <param name="wall"></param>
        /// <param name="element"></param>
        /// <param name="doc"></param>
        private static void ReadWallType(ECWall eCWall,Wall wall, Element element, Document doc)
        {
            string name = element.Name;//типоразмер стены??? == wall.Name;
            name = wall.Name;
            if (DictWallType.TryGetValue(name, out ECWallType wt))
            {
                eCWall.WallType= wt;
                return;
            }
            wt = new ECWallType();
            DictWallType[name] = wt;

            wt.Name= name;
            eCWall.Name = name;

            ElementId typeId = wall.GetTypeId();


            EWallTypes eWallTypes = EWallTypes.None;
            //здесь можно было бы настроить чтобы понимать жб стена несущая кирпичная или газобетонная
            double widthFoot = wall.Width;// — в футах(внутренние единицы)
            double widthMM = RevitAPI.ToMm(widthFoot);
            wt.WidthM = widthMM / 1000;

            //начинаем анализировать EWallTypes

            if (name.Contains("Газобетон"))
            {
                eWallTypes = EWallTypes.AR_Aerocrete;
            }
            else if (name.Contains("Бетон"))
            {
                eWallTypes = EWallTypes.KR_Reinforcement;
            }
            else if (name.Contains("Кирпич"))
            {
                eWallTypes = EWallTypes.AR_Brick;
            }
            else if (name.Contains("Утеплитель") || name.StartsWith("СШ"))
            {
                eWallTypes = EWallTypes.AR_Uteplitel;
            }
            else if (name.Contains("Отделка"))
            {
                eWallTypes = EWallTypes.AR_Otdelca;
            }
            else if (name.Contains("ПГП") || name.Contains("ГКЛ"))
            {
                eWallTypes = EWallTypes.AR_PGPorGKL;
            }
            wt.EWallTypes = eWallTypes;



            WallType wallType = doc.GetElement(typeId) as WallType;
            if (wallType != null)
            {
               
                ReadECWallTypeLayers(wallType, typeId, wt,doc);
            }

        }
        private static Dictionary<string, ECWallType> DictWallType = new Dictionary<string, ECWallType>();



        private static void ReadECWallTypeLayers(WallType wallType, ElementId typeId, ECWallType wt, Document doc)
        {
            wt.Id = typeId.Value;
            wt.FamilyName = wallType.FamilyName; // напр. "Basic Wall" (Базовая стена)

            CompoundStructure cs = wallType.GetCompoundStructure();
            if (cs == null) return; // у сэндвич-стен (Stacked) структуры нет


            // общая толщина типа (в футах) — надёжнее, чем wall.Width у экземпляра
            wt.WidthM = UnitUtils.ConvertFromInternalUnits(cs.GetWidth(), UnitTypeId.Meters);

            IList<CompoundStructureLayer> apiLayers = cs.GetLayers();
            int varIdx = cs.VariableLayerIndex; // индекс переменного слоя или -1

            List<ECLayerWall> layers = new List<ECLayerWall>();

            for (int i = 0; i < apiLayers.Count; i++)
            {
                CompoundStructureLayer layer = apiLayers[i];

                ECLayerWall ec = new ECLayerWall();
                ec.Index = i;
                ec.WidthM = UnitUtils.ConvertFromInternalUnits(cs.GetLayerWidth(i), UnitTypeId.Meters);
                ec.Function = layer.Function;
                ec.IsCore = cs.IsCoreLayer(i);
                ec.IsVariable = (i == varIdx);
                ec.Wraps = layer.LayerCapFlag;

                ElementId matId = cs.GetMaterialId(i);
                ec.ElementId = matId;
                ec.Id = matId.Value;
                Autodesk.Revit.DB.Material mat = doc.GetElement(matId) as Autodesk.Revit.DB.Material;
                if (mat != null)
                {
                    ec.Name = mat.Name;
                    ec.Element = mat;
                    ec.MaterialClass = mat.MaterialClass;
                    Parameter pDesc = mat.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
                    ec.MaterialDescription = (pDesc != null && pDesc.HasValue) ? pDesc.AsString() : null;
                    ec.MaterialColor = mat.Color;

                    // Плотность: у самого Material её нет — она лежит в структурном активе
                    PropertySetElement psa = doc.GetElement(mat.StructuralAssetId) as PropertySetElement;
                    if (psa != null)
                    {
                        Parameter pDens = psa.get_Parameter(BuiltInParameter.PHY_MATERIAL_PARAM_STRUCTURAL_DENSITY);
                        if (pDens != null && pDens.HasValue && pDens.StorageType == StorageType.Double)
                            ec.DensityKgM3 = pDens.AsDouble(); // кг/м³
                    }

                    // Теплопроводность — из теплового актива
                    PropertySetElement pseThermal = doc.GetElement(mat.ThermalAssetId) as PropertySetElement;
                    if (pseThermal != null)
                    {
                        ThermalAsset ta = pseThermal.GetThermalAsset();
                        ec.ThermalConductivityWmK = ta.ThermalConductivity; // Вт/(м·К)
                    }
                }

                layers.Add(ec);
            }
          
            wt.ECLayerWalls = layers;

        }

        public static void GetRooms(ECWall eCWall, Wall wall, Element element, Document doc)
        {
            GetRoomAtSide(eCWall, wall, element, doc, true);//1 сторона
            GetRoomAtSide(eCWall, wall, element, doc, false);//2 сторона
        }
        // комнаты по обе стороны стены (или null, если с этой стороны нет помещения)
        private static double offsetRoomF = UnitUtils.ConvertToInternalUnits(50, UnitTypeId.Millimeters);//мм
        public static void  GetRoomAtSide(ECWall eCWall, Wall wall, Element element, Document doc, bool exteriorSide)
        {
            LocationCurve lc = wall.Location as LocationCurve;
            if (lc == null) return;

            Curve curve = lc.Curve;
            XYZ mid = curve.Evaluate(0.5, true);

            // перпендикуляр к оси в плане
            XYZ dir = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            XYZ nrm = new XYZ(-dir.Y, dir.X, 0);          // влево по ходу оси
            if (!exteriorSide) nrm = -nrm;                 // у стен ось: наружу — наружная грань (Flipped! см. ниже)

            double half = wall.Width / 2.0;
            double offset = half + offsetRoomF; // +50 мм запас чтобы найти комнату

            XYZ pt = mid + nrm * offset;
            Room room = doc.GetRoomAtPoint(pt);
            if (room == null) return;
            // GetRoomAtPoint может вернуться из другого уровня — проверьте высоту точки:
            // она должна попадать в вертикальный диапазон помещения (BaseOffset..BaseOffset+Height)
            var ecRoom = GetECRoom(room, doc);
            if (ecRoom == null) return;

            eCWall.ECRooms.Add(ecRoom);
            return;
        }

        public static ECRoom GetECRoom(Room room, Document doc)
        {
            if (room == null) return null;
            long id = room.Id.Value;
            if (DictECRooms.TryGetValue(id, out var ecRoom))
            {
                return ecRoom;
            }
            ecRoom= new ECRoom();
            DictECRooms[id] = ecRoom;
            ecRoom.Id = id;
            ecRoom.ElementId = room.Id;
            ecRoom.Element = room;
            ecRoom.Room=room;
            string name = room.Name;                 // «Кабинет 101» (Name + Number вместе)
            string number = room.Number;             // «101»
            ElementId levelId = room.LevelId;        // уровень, на котором комната
            Level level = doc.GetElement(levelId) as Level;
            double area = room.Area;                 // площадь в кв. футах (внутренние единицы)
            double volume = room.Volume;            // объём в куб. футах
            double perimeter = room.Perimeter;       // периметр (если границы замкнуты)

            // Конвертация в привычные единицы
            double areaM2 = UnitUtils.ConvertFromInternalUnits(area, UnitTypeId.SquareMeters);
            double volM3 = UnitUtils.ConvertFromInternalUnits(volume, UnitTypeId.CubicMeters);
            double perM = UnitUtils.ConvertFromInternalUnits(perimeter, UnitTypeId.Meters);

            ecRoom.Name = name;
            ecRoom.Number = number;
            ecRoom.AreaM2 = areaM2;
            ecRoom.VolumeM3 = volM3;
            ecRoom.PerimeterM = perM;


            return ecRoom;
        }
        private static Dictionary<long,ECRoom> DictECRooms = new Dictionary<long,ECRoom>();

        /// <summary>
        /// Параметры конкретной стены
        /// </summary>
        /// <param name="eCWall"></param>
        /// <param name="wall"></param>
        /// <param name="element"></param>
        /// <param name="doc"></param>
        private static void ReadWall(ECWall eCWall, Wall wall, Element element, Document doc)
        {
            // ============================================================
            // 1. Длина по оси
            // ============================================================
            double lengthFt = 0;

            //параметры конкретной стены
            LocationCurve locCurve = wall.Location as LocationCurve;
            //выбери один из дувух параметров
            if (wall.Location is LocationCurve lc && lc.Curve != null)
            {
                eCWall.Curve = lc.Curve;
                lengthFt = lc.Curve.Length;
            }
            else
            {
                var pLen = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (pLen != null && pLen.HasValue) lengthFt = pLen.AsDouble();
            }
            eCWall.LengthM = UnitUtils.ConvertFromInternalUnits(lengthFt, UnitTypeId.Meters);
            // ============================================================
            // 2. Высота (учитываем привязку к уровню сверху)
            // ============================================================
            double heightFt = GetWallHeight(wall, doc);
            eCWall.HeightM = UnitUtils.ConvertFromInternalUnits(heightFt, UnitTypeId.Meters);


            // ============================================================
            // 3. Чистая площадь с учётом проёмов (из Revit)
            // ============================================================
            var pArea = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
            if (pArea != null && pArea.HasValue)
                eCWall.AreaNetM2 = UnitUtils.ConvertFromInternalUnits(
                    pArea.AsDouble(), UnitTypeId.SquareMeters);

            // ============================================================
            // 4. Объём с учётом проёмов (из Revit)
            // ============================================================
            var pVol = wall.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
            if (pVol != null && pVol.HasValue)
                eCWall.VolumeM3 = UnitUtils.ConvertFromInternalUnits(
                    pVol.AsDouble(), UnitTypeId.CubicMeters);

            // ============================================================
            // 5. Габаритная площадь без вычета проёмов
            //    (Face.Area внешней грани; fallback → Length × Height)
            // ============================================================
            //double grossAreaSqFt = GetWallGrossAreaSqFt(wall);
            //eCWall.AreaGrossM2 = UnitUtils.ConvertFromInternalUnits(
            //    grossAreaSqFt, UnitTypeId.SquareMeters);
            eCWall.AreaGrossM2 = eCWall.LengthM * eCWall.HeightM;

            // ============================================================
            // 6. Площадь проёмов = все отверстия (двери + окна + прочие)
            // ============================================================
            eCWall.OpeningsAreaM2 = Math.Max(0.0, eCWall.AreaGrossM2 - eCWall.AreaNetM2);

            // ============================================================
            // 7. Площади сечения (торцевая) и в плане (вид сверху)
            // ============================================================
            //eCWall.CrossSectionAreaM2 = eCWall.HeightM * eCWall.WidthM; // с торца
            eCWall.PlanAreaM2 = eCWall.LengthM * eCWall.WidthM; // сверху


            // --- Площадь и объём (вычисляемые параметры) ---
            Parameter areaParam = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
            if (areaParam != null && areaParam.HasValue)
                eCWall.AreaNetM2 = UnitUtils.ConvertFromInternalUnits(
                areaParam.AsDouble(),
                UnitTypeId.SquareMeters); // или DisplayUnitType.DUT_SQUARE_METERS для старых версий

            Parameter volumeParam = wall.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
            if (volumeParam != null && volumeParam.HasValue)
                eCWall.VolumeM3 = UnitUtils.ConvertFromInternalUnits(
                volumeParam.AsDouble(),
                UnitTypeId.CubicMeters);

            //определим, является ли стена наружная по названию
           

        }
        private static void ReadWallOtherData(ECWall eCWall, Wall wall, Element element, Document doc)
        {
            //наружная ли это стена пытаемся определить сразу по названию
            InsulationsWalls.InsulationNameWall(eCWall);
            GetRooms(eCWall, wall, element, doc);
        }


        private static double GetWallHeight(Wall wall, Document doc)
        {
            var pTopLevel = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
            ElementId topLevelId = pTopLevel?.AsElementId();

            if (topLevelId != null && topLevelId != ElementId.InvalidElementId)
            {
                Level baseLevel = doc.GetElement(wall.LevelId) as Level;
                Level topLevel = doc.GetElement(topLevelId) as Level;
                if (baseLevel != null && topLevel != null)
                {
                    double baseOff = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0.0;
                    double topOff = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.AsDouble() ?? 0.0;
                    double h = (topLevel.Elevation + topOff) - (baseLevel.Elevation + baseOff);
                    if (h > 1e-6) return h;
                }
            }

            // Не привязана → Unconnected Height
            var pUnconn = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            if (pUnconn != null && pUnconn.HasValue) return pUnconn.AsDouble();

            // Fallback: bounding box
            var bb = wall.get_BoundingBox(null);
            return bb != null ? (bb.Max.Z - bb.Min.Z) : 0.0;
        }
        // ----------------------------------------------------------------
        // Габаритная площадь внешней грани (без вычета проёмов)
        // ----------------------------------------------------------------
        private static double GetWallGrossAreaSqFt(Wall wall)
        {
            //не верно - он с учетом проемов считает
            try
            {
                var sideRefs = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior);
                if (sideRefs != null && sideRefs.Count > 0)
                {
                    if (wall.GetGeometryObjectFromReference(sideRefs[0]) is Face face)
                        return face.Area; // площать в кв.футах, без вычета отверстий
                }
            }
            catch
            {
                // HostObjectUtils иногда не работает для нестандартных стен — молча уходим в fallback
            }

            // Fallback: габаритный прямоугольник
            double h = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0.0;
            double l = (wall.Location as LocationCurve)?.Curve?.Length ?? 0.0;
            return l * h;
        }
        private static void ReadDoorAndWindow(ECWall eCWall, Wall wall, Element element, Document doc)
        {
            //var dependentElements = wall.GetDependentElements(new ElementMulticategoryFilter(
            //new List<BuiltInCategory> { BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows }));
            // FindInserts(true, true, true, true) вернет двери, окна и отверстия
            IList<ElementId> insertIds = wall.FindInserts(true, true, true, true);
            foreach (ElementId depId in insertIds)
            {
                Element doorOrWind = doc.GetElement(depId);
                if (doorOrWind == null) continue;
                if (doorOrWind.Category == null) continue;
                var bic = doorOrWind.Category.BuiltInCategory;

                if(bic!= BuiltInCategory.OST_Doors && bic != BuiltInCategory.OST_Windows) { continue; }
                // Проверяем, что элемент хостится именно в этой стене,
                // а не во встроенной (embedded) стене
                if (doorOrWind is FamilyInstance fi)
                {
                    Element host = fi.Host;
                    if (host == null || host.Id != wall.Id) continue;
                }

                //надо получить окна и двери их размеры габаритные
                var dw = GetECDoorOrWindow(doorOrWind, eCWall, wall,bic);
                if (dw != null)
                {
                    eCWall.ECDoorOrWindows.Add(dw);
                }

            }
        }
        private static Dictionary<long, ECDoorOrWindow> DictECDoorOrWindow = new Dictionary<long, ECDoorOrWindow>();
        private static ECDoorOrWindow GetECDoorOrWindow(Element doorOrWind, ECWall eCWall, Wall wall, BuiltInCategory bic)
        {
            ElementId id = doorOrWind.Id;
            long idL = id.Value;
            if(DictECDoorOrWindow.TryGetValue(idL, out var dw))
            {
                dw.ECWalls.Add(eCWall);//какой стене принадлежит
                //Внимание для стены это явная ошибка если дверь или окно в нескольких стенах!!!!!
                return dw;
            }

             dw = new ECDoorOrWindow
            {
                ElementId = id,
                Id = idL,
                Element = doorOrWind,
                Name = doorOrWind.Name
            };
            dw.ECWalls.Add(eCWall);//какой стене принадлежит

            // --- Габариты: независимый fallback ---
            var widthParam = doorOrWind.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                           ?? doorOrWind.get_Parameter(BuiltInParameter.WINDOW_WIDTH);
            var heightParam = doorOrWind.get_Parameter(BuiltInParameter.DOOR_HEIGHT)
                           ?? doorOrWind.get_Parameter(BuiltInParameter.WINDOW_HEIGHT);

            double widthM = (widthParam != null && widthParam.HasValue)
                ? RevitAPI.ToMm(widthParam.AsDouble()) / 1000.0 : 0.0;
            double heightM = (heightParam != null && heightParam.HasValue)
                ? RevitAPI.ToMm(heightParam.AsDouble()) / 1000.0 : 0.0;

            dw.LengthM = widthM;
            dw.HeightM = heightM;
            dw.AreaM2 = widthM * heightM;
            dw.BuiltInCategory= bic;
            if (wall == null) return dw;

            // --- Отметка низа проёма от уровня ---
            var sillParam = doorOrWind.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
            if (sillParam != null && sillParam.HasValue)
                dw.OtmZ = RevitAPI.ToMm(sillParam.AsDouble()) / 1000.0;

            // --- Проекция на ось стены ---
            if (!(doorOrWind.Location is LocationPoint locPoint)) return dw;
            if (!(wall.Location is LocationCurve locCurve) || locCurve.Curve == null) return dw;

            XYZ instPoint = locPoint.Point;
            Curve wallCurve = locCurve.Curve;

            IntersectionResult projection = wallCurve.Project(instPoint);
            if (projection == null) return dw;

            double p0 = wallCurve.GetEndParameter(0);
            double p1 = wallCurve.GetEndParameter(1);
            double wallLen = wallCurve.Length;
            if (Math.Abs(p1 - p0) < 1e-9 || wallLen < 1e-9) return dw;

            // Нормализованное положение центра
            double normCenter = (projection.Parameter - p0) / (p1 - p0);

            // Расстояние вдоль оси до центра
            double centerDist = normCenter * wallLen;

            // Полуширина в футах
            double halfWidthFt = RevitAPI.ToFoot(widthM) / 2.0;

            // Клампим в пределах стены
            double startDist = Math.Max(0.0, Math.Min(wallLen, centerDist - halfWidthFt));
            double endDist = Math.Max(0.0, Math.Min(wallLen, centerDist + halfWidthFt));

            dw.StartPt = wallCurve.Evaluate(startDist / wallLen, true);
            dw.EndPt = wallCurve.Evaluate(endDist / wallLen, true);

            return dw;
        }
        private static void CalcJoinWalls(ECWall eCWall, Wall wall, Element element, Document doc)
        {
            //ненадежно, может не работать!!!!
            //анализ соединенных стен - только когда все стены заполнены
            IList<ElementId> joinedIds = JoinGeometryUtils.GetJoinedElements(doc, wall).ToList();
            foreach (ElementId id in joinedIds)
            {
                Element elem = doc.GetElement(id);
                if (elem is Wall joinedWall)
                {
                    // это стена, с которой данная стена соединена геометрией
                }
            }
        }

        

    }
}
