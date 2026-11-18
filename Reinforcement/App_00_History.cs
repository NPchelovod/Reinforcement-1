using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    public static class App_History
    {
        /// <summary>
        /// Тут пиши конкретно текущего релиза отличия версии
        /// </summary>
        private static string CurrentVersion = "Кубики сброс согласования не при создании новых элементов, а только при изменении геометрии";

        private static string DifferentZeor = "Отличия новой версии от вашей неизвестны";

        public static UpdateStructVersion NewVersion = null;
       

        /// <summary>
        /// история обновления. Возвращает данные новой версии относительно переданной даты
        /// </summary>
        public static string GetUpdate(DateTime dateTime)
        {
            // не затираем, если dateTime некорректный — оставим то, что уже есть
            if (dateTime == DateTime.MinValue) return DifferentZeor;

            ReadData();
            if (!FileIsRead || UpdateAllVersion?.UpdateStructVersion == null) return DifferentZeor;

            var entry = UpdateAllVersion.UpdateStructVersion
                .Where(x => x.DateTimeDll <= dateTime)
                .OrderByDescending(x => x.DateTimeDll)
                .FirstOrDefault();

            if (entry == null || string.IsNullOrEmpty(entry.Message) || entry.Message == CurrentVersion)
                return DifferentZeor;
            //NewVersion = entry;
            
            return entry.Message;
        }

        /// <summary>
        /// Вызов для записи нового изменения в плагине, инициирует запущенный плагин от автора
        /// </summary>
        public static void WriteOurVersion()
        {
            ReadData();
            if (!App_Folders.UserIsAvtor) return;
            if (!FolderExist) return;
            if (!FileNotContainsCurrentVersion && FileIsExist) return;

            string path = GetFilePath;
            string tmp = Path.Combine(FolderVersion, VersionFileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                // List сохраняет порядок — сортируем один раз перед записью
                UpdateAllVersion.UpdateStructVersion = UpdateAllVersion.UpdateStructVersion
                    .OrderByDescending(x => x.DateTimeDll)
                    .ToList();

                string json = JsonSerializer.Serialize(UpdateAllVersion, RevitAPI.Options);
                File.WriteAllText(tmp, json);
                try { File.Replace(tmp, path, null, true); }
                catch (FileNotFoundException) { File.Move(tmp, path); }
                FileNotContainsCurrentVersion = false;
                FileIsExist = true;
            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); }
                catch (Exception ex)
                {
                    AppErrors.LogError(ex);
                }
            }
        }

        private static void ReadData()
        {
            FolderExist = false;
            FileIsRead = false;
            FileNotContainsCurrentVersion = false;
            FileIsExist = false;
            UpdateAllVersion = new UpdateAllVersion();
            NewVersion = null;
           

            if (!FastDirectoryChecker.DirectoryExistsFast(FolderVersion)) return;
            FolderExist = true;

            var path = GetFilePath;
            if (File.Exists(path))
            {
                FileIsExist = true;
                try
                {
                    string json = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var data = JsonSerializer.Deserialize<UpdateAllVersion>(json, RevitAPI.Options);
                        if (data?.UpdateStructVersion != null)
                        {
                            UpdateAllVersion = data;
                            FileIsRead = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppErrors.LogError(ex);
                }
            }

            DateTime dllDate = App_Folders.DatePluginDll;

            // добавляем запись о текущей версии, если её нет (и если файла не было — тоже)
            if (dllDate != DateTime.MinValue &&
                !UpdateAllVersion.UpdateStructVersion.Any(x => x.DateTimeDll == dllDate))
            {
                UpdateAllVersion.UpdateStructVersion.Add(new UpdateStructVersion
                {
                    DateTimeDll = dllDate,
                    WriteInJson = DateTime.Now,
                    IsAvtor = App_Folders.UserIsAvtor,
                    Message = CurrentVersion
                });
                FileNotContainsCurrentVersion = true;
            }

            // ближайшая запись новее текущей DLL — для окна настроек
            var upData = UpdateAllVersion.UpdateStructVersion
                .Where(x => x.DateTimeDll > dllDate)
                .OrderByDescending(x => x.DateTimeDll)
                .FirstOrDefault();

            if (upData != null && !string.IsNullOrEmpty(upData.Message))
            {
                NewVersion = upData;
            }
        }

        private static volatile bool FolderExist = false;
        private static volatile bool FileIsExist = false;
        private static volatile bool FileIsRead = false;
        private static volatile bool FileNotContainsCurrentVersion = false;

        private static UpdateAllVersion UpdateAllVersion = null;

        private static string FolderVersion = @"Y:\\Revit\\_ЕС BIM_Плагин\\0_Разработчику";
        private static string VersionFileName = "Отличия версий.json";
        private static string GetFilePath => Path.Combine(FolderVersion, VersionFileName);
    }

    public class UpdateStructVersion
    {
        public DateTime DateTimeDll { get; set; } = DateTime.MinValue;
        public DateTime WriteInJson { get; set; } = DateTime.MinValue;
        public bool IsAvtor { get; set; } = false;
        public string Message { get; set; } = null;
    }

    public class UpdateAllVersion
    {
        // свойство вместо поля — работает без IncludeFields, но с твоими опциями и так ок
        public List<UpdateStructVersion> UpdateStructVersion { get; set; } = new List<UpdateStructVersion>();
    }
}