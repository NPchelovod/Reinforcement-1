using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace Reinforcement
{
    public abstract class RevitCommandBase : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData?.Application?.ActiveUIDocument == null)
                { message = "Откройте проект Revit перед запуском команды."; return Result.Failed; }
                RevitAPI.Initialize(commandData);
                return ExecuteCore(commandData.Application.ActiveUIDocument);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception error)
            {
                message = error.Message;
                try { App_Apdater_1.AppErrors.LogError(error); } catch { }
                return Result.Failed;
            }
        }
        protected abstract Result ExecuteCore(UIDocument document);
    }
}
