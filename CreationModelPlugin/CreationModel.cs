using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
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
            Wall walls = CreateWalls(doc, level1, level2, wall);

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

        public Wall CreateWalls(Document doc, Level level1, Level level2, Wall wall)
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

            List<Wall> walls = new List<Wall>();
            //Wall wall = new Wall();

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
    }
}
