using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Net;
using System.Windows.Controls;
using System.Text.RegularExpressions;
namespace Reinforcement
{
    public class OV_Panel_Calculate
    {
        //имена семейств на виде для сопоставления
        public static HashSet<string> NamesOvConstruct = new HashSet<string>()
        {
            "Воздуховод", "Переходник", "BIMLIB", "глушитель", "Отвод","Врезка"
        };

        //признак врезки значит ищем сопоставлнеи координатной линии
        public static HashSet<string> NamesOvVrezka = new HashSet<string>()
        {
            "Врезка"
        };


        //где прописана Имя системы
        public static HashSet<string> NamesSystem = new HashSet<string>()
        {
            "Имя системы"
        };


        //у кого есть это свойство то начало графа или его доп ветвь
        public static HashSet<string> NamesRasxodAir = new HashSet<string>()
        {
            "ADSK_Расход воздуха","Дополнительный расход" //для добавки к ветке
        };

        //потеря давлния воздуха
        public static HashSet<string> NamesLockAir = new HashSet<string>()
        {
            "ADSK_Потеря давления","Падение давления"
        };

        //где праписана ветвь системы П2 или П1
        public static HashSet<string> BrenchGraph = new HashSet<string>()
        {
            "Комментарии" // значит коменты должны юыть П2, П2...
        };



        //куда записываем наши данные
        public static HashSet<string> WriterPrimeh = new HashSet<string>()
        {
            "ADSK_Примечание"
        };

        public HashSet<Element> Elements = new HashSet<Element>();
        public HashSet<ElementData> ElementDatas = new HashSet<ElementData>();
        public static Document doc = null;
        public OV_Panel_Calculate()
        {
            //графопостроитель
            doc = RevitAPI.Document;
            //получаем активный вид и все его элементы
            UIDocument uidoc = RevitAPI.UiDocument;  // Для доступа к ActiveView
            View activeView = uidoc.ActiveView;

            FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
            IList<Element> allElementsInActiveView = collector.ToElements();

            //1 - находим все элементы ов

            foreach (var element in allElementsInActiveView)
            {
                // ✅ 1. Получить тип элемента
                ElementType elementType = doc.GetElement(element.GetTypeId()) as ElementType;

                if (elementType == null) { continue; }
                

                // ✅ 2. Имя типоразмера
                string typeName = elementType.Name;

                // ✅ 3. Имя семейства  
                string familyName = elementType.FamilyName;


                foreach(string nameFam in NamesOvConstruct)
                {
                    if(familyName.Contains(nameFam))
                    {

                        if(Elements.Add(element))
                        {
                            ElementDatas.Add(new ElementData(element, familyName, typeName));
                        }
                        break;
                    }
                }

            }

            if(ElementDatas.Count==0)
            {
                //выводим сообщения что ничего не найдено
                return;
            }
            //2 эти элементы строим граф по координатам

            var elementsDatasList = ElementDatas.ToList();

            //теперь ищем самые близкие
            for(int i = 0; i < elementsDatasList.Count; i++)
            {
                ElementData elementData1 = elementsDatasList[i];
                ElementData elementDataBetter=null;

                var elementDatasBetter = new HashSet<ElementData>();
               
                double minDist = errorDistance + 1;
                for (int j = i+1; j < elementsDatasList.Count; j++)
                {
                    ElementData elementData2 = elementsDatasList[j];
                     
                    //ищем дистанцию
                    bool betterED=false;
                    foreach(XYZ xYZ1 in  elementData1.XYZsMM)
                    {
                        foreach (XYZ xYZ2 in elementData2.XYZsMM)
                        {
                            double distance = xYZ1.DistanceTo(xYZ2);
                            //то все, в цепочке э
                            if (distance <= minDist)
                            {
                                minDist = distance;
                                betterED = true;
                            }
                        }
                    }
                    if(betterED)
                    {
                        elementDataBetter = elementData2;
                        if (minDist < 1)
                        {
                            elementDatasBetter.Add(elementData1);
                        }
                    }
                }
                if (elementDataBetter == null) { continue; }

                elementDatasBetter.Add(elementDataBetter);


                foreach (var elementData in elementDatasBetter)
                {
                    elementData1.NearlyElements.Add(elementData);
                    elementData.NearlyElements.Add(elementData1);
                }

                //иначе значи мы нашли пару, печаль в том что пар может быть несколько???

            }

            //собрав надо построить графы - самое сложное кто с кем пара
            WaysSeach();
        }



        public void WaysSeach()
        {
            HashSet<ElementData> elementDatasPast = new HashSet<ElementData>();


            //вычлиняем тех у кого только один сосед
            var initialsWays = ElementDatas.Where(x => x.NearlyElements.Count == 1).OrderByDescending(x=>x.RasxodAir).ToHashSet();

            
            foreach (var elementData in initialsWays)
            {
                //
                if (elementDatasPast.Contains(elementData)) {  continue; }

                
            }
        }




        public static double errorDistance = 100;// погрешность поиска совпадений мм/304.8 для дюймов
        public static double convertMM = 304.8;// mm in dyem
        public class ElementData
        {
            
            public List<XYZ> XYZs = new List<XYZ>();//дюймы!!!!
            public List<XYZ> XYZsMM = new List<XYZ>();

            public Element Element { get; set; }

            public bool Vrezka = false;
            public string NameSystem = "";

            public double RasxodAir = 0;
            public double LockAir = 0;


            public HashSet<ElementData> NearlyElements = new HashSet<ElementData>();


            public ElementData(Element element, string familyName, string typeName)
            {
                this.Element = element;

               

                LocationCurve locCurve = element.Location as LocationCurve;
                if (locCurve != null)
                {
                    Curve curve = locCurve.Curve;  // ✅ Главная кривая элемента

                    // 1. ТОЧКИ НАЧАЛА И КОНЦА
                    XYZ startPoint = curve.GetEndPoint(0);  // Начало (параметр 0)
                    XYZ endPoint = curve.GetEndPoint(1);    // Конец (параметр 1)

                    // 2. ДЛИНА
                    double length = curve.Length;  // Общая длина в футах

                    // 3. ТЕКУЩИЙ ПАРАМЕТР (0.0 - начало, 1.0 - конец)
                    double parameter = 0.5;  // Середина
                    XYZ midPoint = curve.Evaluate(parameter, true);  // Точка по параметру

                    XYZs = new List<XYZ> { startPoint, endPoint };
                }
                else
                {
                    LocationPoint locPoint = element.Location as LocationPoint;
                    if (locPoint != null)
                    {
                        XYZ point = locPoint.Point;  // X, Y, Z координаты
                        XYZs = new List<XYZ> { point };
                    }
                    else
                    {
                        BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                        if (bbox != null)
                        {
                            XYZ minPoint = bbox.Min;  // Левый нижний угол
                            XYZ maxPoint = bbox.Max;  // Правый верхний угол
                            XYZs = new List<XYZ> { minPoint, maxPoint };

                        }
                    }

                }
                //перевод в мм
                foreach (XYZ xYZ in XYZs)
                {
                    XYZsMM.Add(new XYZ(xYZ.X* convertMM, xYZ.Y * convertMM, xYZ.Z * convertMM));
                }


                //врезка ли это
                foreach(var name in NamesOvVrezka)
                {
                    if(familyName.Contains(name))
                    {
                        Vrezka=true;
                        break;
                    }
                }
                //имя системы типо П1 или как
                foreach (var name in NamesSystem)
                {
                    var param = element.LookupParameter(name);
                    if (param != null && param.HasValue)
                    {
                        NameSystem = param.AsString();
                        break;
                    }
                }

                //попытка найти расход воздуха
                foreach (var name in NamesRasxodAir)
                {
                    var param = element.LookupParameter(name);
                    if (param != null && param.HasValue)
                    {
                        string valueStr = param.AsValueString();  // "0,357 м3/ч"
                        Match match = Regex.Match(valueStr, @"[\d,]+");
                        if (match.Success)
                        {
                            RasxodAir = double.Parse(match.Value.Replace(",", "."));
                            // RasxodAir = 0.357 ✅
                            break;
                        }
                    }
                }

                //в паскалях потеря давления
                foreach (var name in NamesLockAir)
                {
                    var param = element.LookupParameter(name);
                    if (param != null && param.HasValue)
                    {
                        string valueStr = param.AsValueString();  // "0,357 м3/ч"
                        Match match = Regex.Match(valueStr, @"[\d,]+");
                        if (match.Success)
                        {
                            LockAir = double.Parse(match.Value.Replace(",", "."));
                            break;
                        }
   
                    }
                }
            }

        }


        public class GroupOVElements
        {
            //public HashSet<>
        }
        public Dictionary<Element, GroupOVElements> NearlyElements = new Dictionary<Element, GroupOVElements>();
    
    }
}
