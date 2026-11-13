using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
namespace Reinforcement
{
    /// <summary>
    /// Утилитный класс для глобального доступа к Revit API-объектам.
    /// Используется в качестве фасада к UIApplication, UIDocument и Document.
    /// </summary>
    public static class RevitAPI
    {
       

        //предлагаемое имя транзакции чтобы мы могли найти что оно - наше
        public static string GetTranzactionName(string nameMethod)
        {
            return $"{App.TabName}_{nameMethod}";
        }
        //предлагаемое имя процесса, чтобы мы могли потом узнать что оно - наше
        public static string GetProcessName(string nameMethod)
        {
            return $"{App.TabName}_{nameMethod}";
        }
        //наша ли это транзакция
        public static bool OurTranzaction(string tranzactionName) { return tranzactionName.StartsWith(App.TabName); }

        private static UIApplication _uiApplication=null;

        /// <summary>
        /// Проверяет, был ли класс инициализирован через Initialize().
        /// </summary>
        public static bool IsInitialized => _uiApplication != null;

        /// <summary>
        /// Активное приложение Revit.
        /// </summary>
        public static UIApplication UiApplication
        {
            get
            {
                if (!IsInitialized)
                    throw new InvalidOperationException("RevitAPI не инициализирован. Вызовите Initialize() перед использованием.");
                return _uiApplication;
            }
        }

        /// <summary>
        /// Активный UIDocument (интерфейсный документ).
        /// </summary>
        public static UIDocument UiDocument => UiApplication.ActiveUIDocument
            ?? throw new InvalidOperationException("Откройте документ Revit перед запуском команды.");

        /// <summary>
        /// Текущий Revit-документ.
        /// </summary>
        public static Document Document => UiDocument.Document;

        /// <summary>
        /// Инициализация класса контекстом команды Revit.
        /// Вызывается один раз из IExternalCommand.Execute().
        /// </summary>
        /// <param name="commandData">Данные внешней команды</param>
        public static void Initialize(ExternalCommandData commandData)
        {

            if (commandData == null)
                throw new ArgumentNullException(nameof(commandData));

            
            Initialize(commandData.Application);
            StatUsers.Update();
        }
        public static void Initialize(UIApplication uIApplication)
        {
            _uiApplication = uIApplication ?? throw new ArgumentNullException(nameof(uIApplication));
        }
        /// <summary>
        /// Перевод из внутренних единиц Revit (футы) в миллиметры.
        /// </summary>
        public static double ToMm(double number)
        {
            return UnitUtils.ConvertFromInternalUnits(number, UnitTypeId.Millimeters);
        }

        /// <summary>
        /// Перевод из миллиметров в футы (внутренние единицы Revit).
        /// </summary>
        public static double ToFoot(double number)
        {
            return UnitUtils.ConvertToInternalUnits(number, UnitTypeId.Millimeters);
        }


        public static bool RequestPassword()
        {
            if (App_Folders.UserIsAdmin) { return App_Folders.UserIsAdmin; }//уже верный пароль

            using (var form = new PasswordForm())
            {
                var revitHandle = UiApplication.MainWindowHandle;
                var owner = new RevitWindowHandle(revitHandle);

                var result = form.ShowDialog(owner);

                if (result != DialogResult.OK)
                    return false;
                App_Folders.UserIsAdmin = IsPasswordCorrect(form.EnteredPassword);
                // Пароль верный — продолжаем
                TaskDialog.Show("OK", "Пароль принят.");
                return App_Folders.UserIsAdmin;
            }
        }

        private static bool IsPasswordCorrect(string entered)
        {
            if (string.IsNullOrEmpty(entered))
                return false;

            // Простейший вариант — сравнение с константой.
            // Для продакшена используйте хеш (см. ниже).
            return entered == App_Folders.Password;
        }
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            IncludeFields = true,
            WriteIndented = true
        };

        private static readonly Dictionary<Document, DocumentInfo> _cache = new Dictionary<Document, DocumentInfo>();

        public static DocumentInfo GetDocumentInfo(Document doc)
        {
            if (doc == null) return null;
            if (_cache.TryGetValue(doc, out var info)) return info;

            info = Build(doc);
            _cache[doc] = info;
            return info;
        }

        public static void Remove(Document doc)
        {
            if (doc != null) _cache.Remove(doc);
        }

        public static void Clear() => _cache.Clear();

        private static DocumentInfo Build(Document doc)
        {
            var info = new DocumentInfo
            {
                Title = doc.Title,
                IsWorkshared = doc.IsWorkshared,
            };

            // 1. Central path (workshared)
            try
            {
                var central = doc.GetWorksharingCentralModelPath();
                if (central != null)
                {
                    string cp = ModelPathUtils.ConvertModelPathToUserVisiblePath(central);
                    if (!string.IsNullOrEmpty(cp))
                    {
                        info.CentralPath = cp;
                        info.HasCentral = true;
                    }
                }
            }
            catch { /* центральная недоступна — идём к локальному */ }

            // 2. Путь / имя
            string fullPath = info.HasCentral ? info.CentralPath : doc.PathName;
            info.IsSaved = !string.IsNullOrEmpty(fullPath);

            if (info.IsSaved)
            {
                info.FullPath = fullPath;
                info.FileName = Path.GetFileName(fullPath);
                info.Folder = Path.GetDirectoryName(fullPath);
                info.Name = CleanName(Path.GetFileNameWithoutExtension(fullPath));
            }
            else
            {
                info.Name = CleanName(doc.Title);
            }

            // 3. GUID'ы — обязательны для дедупликации между сессиями
            try
            {
                var pi = doc.ProjectInformation;
                if (pi != null)
                {
                    string uid = pi.UniqueId;
                    if (!string.IsNullOrEmpty(uid)) info.ProjectUniqueId = uid;
                }
            }
            catch { /* ProjectInformation иногда недоступен в момент события */ }

            try
            {
                // CreationGUID — Guid, у только что созданного документа может быть Empty
                Guid cg = doc.CreationGUID;
                if (cg != Guid.Empty) info.CreationGuid = cg.ToString();
            }
            catch { }

            // 4. Warnings — дорого (перечисление всех failure'ов), снимаем один раз
            info.WarningsCount = TryCountWarnings(doc);
            info.WarningsSnapshotUtc = DateTime.UtcNow;

            return info;
        }

        private static int TryCountWarnings(Document doc)
        {
            try
            {
                // GetWarnings() возвращает IList<FailureMessage> — .Count дешевле, чем .Count()
                // (LINQ .Count() пробегает по коллекции; на IList есть быстрый путь, но лучше явно)
                var w = doc.GetWarnings();
                return w?.Count ?? 0;
            }
            catch { return 0; }
        }

        private static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string sfx = "_" + App_Folders.UserName;
            return name.EndsWith(sfx, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - sfx.Length)
                : name;
        }


    }


    public sealed class DocumentInfo
    {
        // Идентификация
        public string Name { get; set; }   // "КР_Подзолкова" (без суффикса пользователя)
        public string FullPath { get; set; }   // "\\server\...\КР_Подзолкова.rvt" (или локальный)
        public string Folder { get; set; }   // "\\server\...\"
        public string FileName { get; set; }   // "КР_Подзолкова.rvt"
        public string Title { get; set; }   // doc.Title

        // Флаги
        public bool IsSaved { get; set; }
        public bool IsWorkshared { get; set; }
        public bool HasCentral { get; set; }
        public string CentralPath { get; set; }

        // GUID'ы и метрики
        public string ProjectUniqueId { get; set; }   // ProjectInformation.UniqueId (Guid проекта)
        public string CreationGuid { get; set; }   // doc.CreationGUID — уникален для каждого «рождения» документа
        public int WarningsCount { get; set; }   // снимок на момент Build

        // Время снимка warnings — полезно, чтобы понимать «свежесть»
        public DateTime WarningsSnapshotUtc { get; set; }
    }

}

