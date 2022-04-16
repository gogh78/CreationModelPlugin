using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            Level level1 = ListLevel(doc)
                .Where(x => x.Name.Equals("Уровень 1"))  // выбираем уровень 1 из списка
                .FirstOrDefault();

            Level level2 = ListLevel(doc)
                .Where(x => x.Name.Equals("Уровень 2"))  // выбираем уровень 2 из списка
                .FirstOrDefault();

            Wall wall = null;
            List<Wall> walls = new List<Wall>();
            CreateWalls(doc, level1, level2, wall, walls);
            AddDoor(doc, level1, walls[0]);
            AddWindow(doc, level1, walls[1]);
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);

            return Result.Succeeded;
        }

        public List<Level> ListLevel(Document doc)
        {
            List<Level> listLevel = new FilteredElementCollector(doc) //создаем список уровней
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            return listLevel;
        }

        public Wall CreateWalls(Document doc, Level level1, Level level2, Wall wall, List<Wall> walls)
        {
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);  //ширина здания
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);   //глубина здания
            double dx = width / 2; //координаты точки по х относительно центра
            double dy = depth / 2; //координаты точки по y относительно центра

            List<XYZ> points = new List<XYZ>(); //определяем координаты точек углов здания
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));  //замыкаем координаты на первую точку

            //List<Wall> walls = new List<Wall>();

            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);   //создаем линию для построения стены
                wall = Wall.Create(doc, line, level1.Id, false);          //строим стену на основе линии
                walls.Add(wall);                                          // добавляем стену в список
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id); //определяем верх стены на высоте 2-го уровня
            }
            transaction.Commit();
            return wall;
        }

        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            Transaction transaction = new Transaction(doc, "Построение дверей");
            transaction.Start();
            LocationCurve hostCurve = wall.Location as LocationCurve;  //определение линии стены
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);
            transaction.Commit();
        }

        private void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0406 x 1220 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            Transaction transaction = new Transaction(doc, "Построение окон");
            transaction.Start();
            LocationCurve hostCurve = wall.Location as LocationCurve;  //определение линии стены
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;
            double z = UnitUtils.ConvertToInternalUnits(800, UnitTypeId.Millimeters);
            XYZ location = new XYZ(point.X, point.Y, z);

            if (!windowType.IsActive)
                windowType.Activate();

            doc.Create.NewFamilyInstance(location, windowType, wall, level1, StructuralType.NonStructural);
            transaction.Commit();
        }
    }
}
