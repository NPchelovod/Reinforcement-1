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

namespace Reinforcement.Stage1.DecorViewPlan
{
    public static class MoveTextInDimension
    {
        public static void Move (Dimension dimension, double viewScale)
        {
            if (dimension.NumberOfSegments == 0)
            {
                if (dimension.Value < RevitAPI.ToFoot(300))
                {
                    XYZ oldTextPosition = dimension.TextPosition;
                    XYZ newTextPosition = new XYZ(oldTextPosition.X - RevitAPI.ToFoot(4 * viewScale), oldTextPosition.Y, oldTextPosition.Z);
                    dimension.TextPosition = newTextPosition;
                }
            }
            else
            {
                var listSegments = dimension.Segments
                    .Cast<Dimension>()
                    .OrderBy(x => x.Value);//from min to max value


            } //move dim text to left or right if value smaller than 300 mm

        }
    }
}
