using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class FindHatchesWithColor : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            View activeView = doc.ActiveView;

            // Список для хранения результатов
            List<List<XYZ>> hatchCoordinates = new List<List<XYZ>>();

            // Поиск всех ImportInstance в документе
            FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id)
            .OfClass(typeof(ImportInstance));

            foreach (ImportInstance importInstance in collector)
            {
                // Получение геометрии DWG
                GeometryElement geometryElement = importInstance.get_Geometry(new Options());

                foreach (GeometryObject geoObject in geometryElement)
                {
                    if (geoObject is GeometryInstance geometryInstance)
                    {
                        // Извлекаем внутреннюю геометрию
                        GeometryElement instanceGeometry = geometryInstance.GetInstanceGeometry();
                        foreach (GeometryObject instanceObject in instanceGeometry)
                        {
                            if (instanceObject is Solid solid)
                            {
                                // Проверяем грани Solid
                                foreach (Face face in solid.Faces)
                                {
                                    try
                                    {
                                        OverrideGraphicSettings overrides = doc.ActiveView.GetElementOverrides(importInstance.Id);
                                        Color color = overrides.ProjectionLineColor;

                                        // Проверка цвета
                                        if (color != null && color.IsValid && color.Red == 40 && color.Green == 0 && color.Blue == 0)
                                        {
                                            // Сохраняем координаты грани
                                            List<XYZ> faceVertices = GetFaceVertices(face);
                                            hatchCoordinates.Add(faceVertices);
                                        }
                                    }
                                    catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                                    {
                                        TaskDialog.Show("Ошибка", "Цвет грани недоступен: " + ex.Message);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Выводим координаты для отладки
            foreach (List<XYZ> coordinates in hatchCoordinates)
            {
                TaskDialog.Show("Координаты штриховки", string.Join("\n", coordinates));
            }

            return Result.Succeeded;
        }

        private List<XYZ> GetFaceVertices(Face face)
        {
            List<XYZ> vertices = new List<XYZ>();
            Mesh mesh = face.Triangulate(); // Триангулируем для получения координат вершин
            foreach (XYZ vertex in mesh.Vertices)
            {
                vertices.Add(vertex);
            }
            return vertices;
        }
    }
}
