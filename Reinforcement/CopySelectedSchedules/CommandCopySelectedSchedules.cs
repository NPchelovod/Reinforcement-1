﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement.CopySelectedSchedules
{
    [Transaction(TransactionMode.Manual)]

    public class CommandCopySelectedSchedules : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;


            var viewModel = new ViewModelCopySelectedSchedules();
            var window = new ViewCopySelectedSchedules(viewModel);

            viewModel.CloseRequest += (s, e) => window.Close();
            window.ShowDialog();

            if (viewModel.DialogResult == true)
            {
                return Result.Succeeded;
            }
            else
            {
                return Result.Cancelled;
            }

        }

    }
}
