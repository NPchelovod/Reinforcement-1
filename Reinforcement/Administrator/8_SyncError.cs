using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class SyncError : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;

            if (doc == null)
            {
                message = "Документ не открыт.";
                return Result.Failed;
            }

            if (!doc.IsWorkshared)
            {
                message = "Документ не является рабочим — синхронизация невозможна.";
                return Result.Failed;
            }

            var transactOptions = new TransactWithCentralOptions();
            var syncOptions = new SynchronizeWithCentralOptions
            {
                SaveLocalAfter = true,
                Comment = "Синхронизация через API (SyncError)"
            };
            syncOptions.SetRelinquishOptions(new RelinquishOptions(true));

            try
            {
                doc.SynchronizeWithCentral(transactOptions, syncOptions);
                return Result.Succeeded;
            }
            catch (CentralModelContentionException ex)   // наследуется от CentralModelException — ловим первым
            {
                LogSyncFailure(doc, $"Конфликт при синхронизации: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
            catch (CentralFileCommunicationException ex)
            {
                LogSyncFailure(doc, $"Ошибка связи с центральной моделью: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
            catch (RevitServerCommunicationException ex)
            {
                LogSyncFailure(doc, $"Ошибка связи с Revit Server: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
            catch (CentralModelException ex)
            {
                LogSyncFailure(doc, $"CentralModelException: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
            {
                LogSyncFailure(doc, $"Синхронизация отменена: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
            catch (Exception ex)
            {
                LogSyncFailure(doc, $"{ex.GetType().Name}: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static void LogSyncFailure(Document doc, string description)
        {
            try
            {
                string docPath = null;
                try
                {
                    var central = doc.GetWorksharingCentralModelPath();
                    docPath = central != null
                        ? ModelPathUtils.ConvertModelPathToUserVisiblePath(central)
                        : doc.PathName;
                    if (string.IsNullOrEmpty(docPath)) docPath = doc.Title;
                }
                catch { }

                FailureBuffer.Add(new FailureRecord
                {
                    Time = DateTime.Now,
                    UserName = App_Folders.UserName,
                    DocPath = docPath,
                    Description = description,
                    Severity = "SyncError",
                    DefinitionId = Guid.Empty
                });
            }
            catch
            {
                // статистика не должна ронять команду
            }
        }
    }
}