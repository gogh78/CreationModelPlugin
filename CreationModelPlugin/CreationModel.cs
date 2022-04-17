using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;

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
            //AddFootPrintRoof(doc, level2, walls);
            AddExtrusionRoof(doc, level2, walls);

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

        private void AddFootPrintRoof(Document doc, Level level2, List<Wall> walls) //Построение крыши по контуру
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;  //задаем смещение контура
            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dt, -dt, 0)); 
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));
            points.Add(new XYZ(-dt, -dt, 0));

            Application application = doc.Application;
            CurveArray footprint = application.Create.NewCurveArray(); //границы плана дома
            for (int i = 0; i < 4; i++)
            {
                LocationCurve curve = walls[i].Location as LocationCurve; //получаем отрезок основы центра стены
                //footprint.Append(curve.Curve); //добавляем отрезок 
                XYZ p1 = curve.Curve.GetEndPoint(0);
                XYZ p2 = curve.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]); //создаем контур с учетом смещения
                footprint.Append(line);
            }
            Transaction transaction = new Transaction(doc, "Построение крыши по контуру");
            transaction.Start();
            ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
            FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintToModelCurveMapping);
            ModelCurveArrayIterator iterator = footPrintToModelCurveMapping.ForwardIterator();
            iterator.Reset();
            while (iterator.MoveNext())
            {
                ModelCurve modelCurve = iterator.Current as ModelCurve;
                footprintRoof.set_DefinesSlope(modelCurve, true);
                footprintRoof.set_SlopeAngle(modelCurve, 0.5);
            }
            foreach(ModelCurve m in footPrintToModelCurveMapping)
            {
                footprintRoof.set_DefinesSlope(m, true);
                footprintRoof.set_SlopeAngle(m, 0.5);
            }
            transaction.Commit();
        }

        private void AddExtrusionRoof(Document doc, Level level2, List<Wall> walls)  //Построение крыши выдавливанием
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();
            
            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;  //задаем смещение контура

            double wallHeight = walls[0].get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
            double roofThickness = roofType.get_Parameter(BuiltInParameter.ROOF_ATTR_DEFAULT_THICKNESS_PARAM).AsDouble();

            LocationCurve curve = walls[3].Location as LocationCurve; //получаем отрезок основы центра стены
            XYZ p1 = new XYZ(curve.Curve.GetEndPoint(0).X , curve.Curve.GetEndPoint(0).Y + dt, wallHeight+ roofThickness);
            XYZ p3 = new XYZ(curve.Curve.GetEndPoint(1).X, curve.Curve.GetEndPoint(1).Y - dt, wallHeight+ roofThickness);
            double z = UnitUtils.ConvertToInternalUnits(800, UnitTypeId.Millimeters);
            XYZ p2 = new XYZ((p1.X + p3.X) / 2, (p1.Y + p3.Y) / 2, wallHeight + roofThickness + z);

            LocationCurve curveExtrusion = walls[0].Location as LocationCurve;
            double extrusionStart = curveExtrusion.Curve.GetEndPoint(0).X- dt;
            double extrusionEnd = curveExtrusion.Curve.GetEndPoint(1).X+ dt;

            CurveArray curveArray = new CurveArray();
            //curveArray.Append(Line.CreateBound(new XYZ(0, 0, 0), new XYZ(0, 20, 20)));
            curveArray.Append(Line.CreateBound(p1, p2));
            //curveArray.Append(Line.CreateBound(new XYZ(0, 20, 20), new XYZ(0, 40, 0)));
            curveArray.Append(Line.CreateBound(p2, p3));

            Transaction transaction = new Transaction(doc, "Построение крыши выдавливанием");
            transaction.Start();
            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, extrusionStart, extrusionEnd);
            transaction.Commit();
        }
    }
}
