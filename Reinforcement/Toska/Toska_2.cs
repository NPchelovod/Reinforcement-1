using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Toska_2 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                // URL с кириллическими символами (будет автоматически преобразован)
                string url = "https://chat.deepseek.com";

                // Создаем процесс для открытия в браузере по умолчанию
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true  // Ключевой параметр для использования оболочки
                };

                Process.Start(psi);
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = $"Ошибка: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}