using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using Autodesk.Revit.DB;
using System.Windows.Markup;

namespace Reinforcement
{
    public static class App_History
    {
        /// <summary>
        /// Тут пиши конкретно текущего релиза отличия версии
        /// </summary>
        private static string CurrentVersion = "Вид настроек чуть лучше";

        private static string DifferentZeor = "Отличия новой версии от вашей неизвестны";
        private static string DifferentEquelPast = "Отличия версии те же, что у прошлой, или не заполнены";
        public static UpdateStructVersion NewVersion = null;
        public static UpdateStructVersion OurVersion = null;

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

                if (UpdateAllVersion.UpdateStructVersion.Count > 1)
                {
                    var pastEl = UpdateAllVersion.UpdateStructVersion[0];
                    //схллопываем повторы не записанной версии
                    for (int i = 1; i < UpdateAllVersion.UpdateStructVersion.Count; i++)
                    {
                        var el = UpdateAllVersion.UpdateStructVersion[i];
                        if (el.Message == pastEl.Message)
                        {
                            el.Message = DifferentEquelPast;
                        }
                    }
                }

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

        private static bool GetFolderExist()
        {
            if (FolderVersion != FolderVersionDefaultPath)
            {
                FolderVersion = FolderVersionDefaultPath;
            }
            if (!FastDirectoryChecker.DirectoryExistsFast(FolderVersion)) 
            {
                
                //пробуем папку temp
                FolderVersion = App_Folders.TempFolder;
                if (!FastDirectoryChecker.DirectoryExistsFast(FolderVersion))
                {
                    FolderVersion = FolderVersionDefaultPath;
                    return false;
                }
               
            }
            return true;
        }
        public static void ReadData()
        {
            FolderExist = false;
            FileIsRead = false;
            FileNotContainsCurrentVersion = false;
            FileIsExist = false;
            UpdateAllVersion = new UpdateAllVersion();
            NewVersion = null;

            if(!GetFolderExist())
            {
                return;
            }

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
                
                var lastPastVersion = UpdateAllVersion.UpdateStructVersion.OrderByDescending(x=>x.DateTimeDll).FirstOrDefault();
                int vPast = 0;
                if (lastPastVersion!=null)
                {
                    vPast = UpdateAllVersion.UpdateStructVersion.Select(x => x.NumVersion).Max();
                    string lastMessage = lastPastVersion.Message;
                    if (CurrentVersion == lastMessage)
                    {
                        CurrentVersion = DifferentEquelPast;
                    }
                }
                
                OurVersion = new UpdateStructVersion
                {
                    NumVersion = vPast+1,
                    DateTimeDll = dllDate,
                    WriteInJson = DateTime.Now,
                    IsAvtor = App_Folders.UserIsAvtor,
                    Message = CurrentVersion
                };
                UpdateAllVersion.UpdateStructVersion.Add(OurVersion);

                //сортируем
                UpdateAllVersion.UpdateStructVersion = UpdateAllVersion.UpdateStructVersion
                    .OrderByDescending(x => x.DateTimeDll)
                    .ToList();
                FileNotContainsCurrentVersion = true;
            }
            else
            {
                //нашу версию читаем, мы чтецы в данном случае
                var ourVersion = UpdateAllVersion.UpdateStructVersion
                .Where(x => x.DateTimeDll == dllDate)
                .OrderByDescending(x => x.DateTimeDll)
                .FirstOrDefault();
                if (ourVersion != null && !string.IsNullOrEmpty(ourVersion.Message))
                {
                    OurVersion = ourVersion;
                }
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

        public static UpdateAllVersion UpdateAllVersion = null;

        private static string FolderVersion = @"Y:\\Revit\\_ЕС BIM_Плагин\\0_Разработчику";
        private static string FolderVersionDefaultPath = @"Y:\\Revit\\_ЕС BIM_Плагин\\0_Разработчику";


        private static string VersionFileName = "Отличия версий.json";
        private static string GetFilePath => Path.Combine(FolderVersion, VersionFileName);
    }

    public class UpdateStructVersion
    {
        public int NumVersion { get; set; }
        public DateTime DateTimeDll { get; set; } = DateTime.MinValue;
        public DateTime WriteInJson { get; set; } = DateTime.MinValue;
        public bool IsAvtor { get; set; } = false;
        public string Message { get; set; } = null;

        public string GetMessegeVersion()
        {
            // Разбиваем по '|', убираем пустые части (если есть лишние разделители подряд)
            var parts = Message.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(p => p.Trim())        // убираем пробелы по краям
                               .Where(p => !string.IsNullOrEmpty(p));

            // Объединяем с переносом строки (для WPF TextBlock это сработает как \n)
            string formattedMessage = string.Join("\n", parts);

            return $"Версия V_{NumVersion} от {DateTimeDll.ToLocalTime():d MMM yyyy, HH:mm}, Обновлено: {formattedMessage}";

        }
    }

    public class UpdateAllVersion
    {
        // свойство вместо поля — работает без IncludeFields, но с твоими опциями и так ок
        public List<UpdateStructVersion> UpdateStructVersion { get; set; } = new List<UpdateStructVersion>();
    }
}