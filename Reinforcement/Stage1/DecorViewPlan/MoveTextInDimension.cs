using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;

namespace Reinforcement.Stage1.DecorViewPlan
{
    public static class MoveTextInDimension
    {
        public static void Move (Dimension dimension, double viewScale, View activeVIew)
        {
            Line line = dimension.Curve as Line;
            var minX = dimension.get_BoundingBox(activeVIew).Min.X;
            var maxX = dimension.get_BoundingBox(activeVIew).Max.X;
            var minY = dimension.get_BoundingBox(activeVIew).Min.Y;
            var maxY = dimension.get_BoundingBox(activeVIew).Max.Y;

            var maxValue = 360;
            var offSet = 5;

            if (Math.Abs(line.Direction.X) == 1)
            {
                if (dimension.NumberOfSegments == 0)
                {
                    if (dimension.Value <= RevitAPI.ToFoot(maxValue))
                    {
                        XYZ oldTextPosition = dimension.TextPosition;
                        XYZ newTextPosition = new XYZ(oldTextPosition.X - RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Y, oldTextPosition.Z);
                        dimension.ResetTextPosition();
                        dimension.TextPosition = newTextPosition;
                    }
                }
                else
                {
                    var list = dimension.Segments
                       .Cast<DimensionSegment>()
                        .OrderBy(x => x.Origin.X)
                        .ToList();
                    int n = 0;
                    while (n + 1 < dimension.NumberOfSegments)
                    {
                        var dim1 = list.ElementAt(n);
                        var dim2 = list.ElementAt(++n);
                        if (dim1.TextPosition.X < dim2.TextPosition.X
                            && dim1.Value <= RevitAPI.ToFoot(maxValue))
                        {
                            XYZ oldTextPosition = dim1.TextPosition;
                            XYZ newTextPosition = new XYZ(oldTextPosition.X - RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Y, oldTextPosition.Z);
                            dim1.TextPosition = newTextPosition;
                        }
                        if(dim1.TextPosition.X > dim2.TextPosition.X
                            && dim1.Value <= RevitAPI.ToFoot(maxValue))
                        {
                            XYZ oldTextPosition = dim1.TextPosition;
                            XYZ newTextPosition = new XYZ(oldTextPosition.X + RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Y, oldTextPosition.Z);

                            dim1.TextPosition = newTextPosition;
                        }
                        if(dim1.TextPosition.X > dim2.TextPosition.X
                            && dim2.Value <= RevitAPI.ToFoot(maxValue))
                        {
                            XYZ oldTextPosition = dim2.TextPosition;
                            XYZ newTextPosition = new XYZ(oldTextPosition.X - RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Y, oldTextPosition.Z);

                            dim2.TextPosition = newTextPosition;
                        }
                        if(dim1.TextPosition.X < dim2.TextPosition.X
                            && dim2.Value <= RevitAPI.ToFoot(maxValue))
                        {
                            XYZ oldTextPosition = dim2.TextPosition;
                            XYZ newTextPosition = new XYZ(oldTextPosition.X + RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Y, oldTextPosition.Z);

                            dim2.TextPosition = newTextPosition;
                        }

                    }

                } //move dim text to left or right if value smaller than maxValue mm
            }
            else if (Math.Abs(line.Direction.Y) == 1)
            {
                if (dimension.NumberOfSegments == 0)
                {
                    if (dimension.Value <= RevitAPI.ToFoot(maxValue))
                    {
                        XYZ oldTextPosition = dimension.TextPosition;
                        XYZ newTextPosition = new XYZ(oldTextPosition.X, oldTextPosition.Y - RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Z);

                        dimension.TextPosition = newTextPosition;
                    }
                }
                else
                {
                    var list = dimension.Segments
                        .Cast<DimensionSegment>()
                        .OrderBy(x => x.Origin.Y)
                        .ToList();
                    int n = 0;
                    while (n + 1 < dimension.NumberOfSegments)
                    {
                        var dim1 = list.ElementAt(n);
                        var dim2 = list.ElementAt(++n);
                        if (dim1.TextPosition.Y < dim2.TextPosition.Y
                            && dim1.Value <= RevitAPI.ToFoot(maxValue))
                        {
                            XYZ oldTextPosition = dim1.TextPosition;
                            XYZ newTextPosition = new XYZ(oldTextPosition.X, oldTextPosition.Y - RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Z);

                            dim1.TextPosition = newTextPosition;
                        }
                        if(dim1.TextPosition.Y > dim2.TextPosition.Y
                            && dim1.Value <= RevitAPI.ToFoot(maxValue))
                        {
                            XYZ oldTextPosition = dim1.TextPosition;
                            XYZ newTextPosition = new XYZ(oldTextPosition.X, oldTextPosition.Y + RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Z);

                            dim1.TextPosition = newTextPosition;
                        }
                        if(dim1.TextPosition.Y > dim2.TextPosition.Y
                            && dim2.Value <= RevitAPI.ToFoot(maxValue))
                        {
                            XYZ oldTextPosition = dim2.TextPosition;
                            XYZ newTextPosition = new XYZ(oldTextPosition.X, oldTextPosition.Y - RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Z);

                            dim2.TextPosition = newTextPosition;
                        }
                         if (dim1.TextPosition.Y < dim2.TextPosition.Y
                            && dim2.Value <= RevitAPI.ToFoot(maxValue))
                        {
                            XYZ oldTextPosition = dim2.TextPosition;
                            XYZ newTextPosition = new XYZ(oldTextPosition.X, oldTextPosition.Y + RevitAPI.ToFoot(offSet * viewScale), oldTextPosition.Z);

                            dim2.TextPosition = newTextPosition;
                        }

                    }
                } //move dim text to left or right if value smaller than maxValue mm


            }
        }
    }
}
