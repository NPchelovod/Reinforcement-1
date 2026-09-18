using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using System.Text.Json;
using Autodesk.Revit.DB;
namespace Reinforcement
{
    public partial class StatLoad
    {
        public List<LookUsers> LookUsersList { get; set; } = new List<LookUsers>();
        public bool ServerOnly { get; set; }=true;
        public  int DaysBack { get; set; } = 100;
        public int DaysFreshBack { get; set; } = 0;
        //public StatLoad(string folderPath = null) 
        //{
        //    //загрузка файлов статистики всех пользователей
        //    LoadFiles(folderPath);
        //    GetFilesProgress();
        //    GetStatistics();
        //}
        public StatLoad(bool serverOnly = true, int daysBack =100,int daysFreshBack=0, string folderPath = null)
        {
            ServerOnly = serverOnly;
            DaysBack = daysBack;
            DaysFreshBack = daysFreshBack;

            LoadFiles(folderPath);
            GetFilesProgress();
            GetStatistics();
        }
        public string FolderPathStatistics;
        public bool LoadFiles(string folderPath = null)
        {
            LookUsersList = new List<LookUsers>();
            if (string.IsNullOrEmpty(folderPath))
            {
                //путь смотрим в каталоге нашем
                folderPath = LookUsers.folderStatistics;
                if (string.IsNullOrEmpty(folderPath))
                { return false; }
            }
            if (!Directory.Exists(folderPath))
                return false;
            FolderPathStatistics = folderPath;

            var options = LookUsers.Options;
            foreach (var file in Directory.EnumerateFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    if (string.IsNullOrWhiteSpace(json))
                        continue;

                    var data = JsonSerializer.Deserialize<LookUsers>(json, options);
                    if (data != null)
                    {
                        LookUsersList.Add(data);
                        if(string.IsNullOrEmpty(data.UserName))
                        {
                            string name = Path.GetFileNameWithoutExtension(file);
                            int idx = name.IndexOf('_');
                            data.UserName = idx > 0 ? name.Substring(0, idx) : name;
                        }
                    }
                }
                catch
                {
                    // битый файл — пропускаем, не роняем всю операцию
                }
            }
            if (LookUsersList.Count > 0)
            {
                return true;
            }
            return false;
        }

        private static string PrefixServer = "RSN";

        public DateTime DateTimeNow = DateTime.Now;
        public void GetFilesProgress()
        {
            DictFileDataLook = new Dictionary<string, FileDataLook>();
            int anuser = 0;
            foreach (var lj in LookUsersList)
            { 
                string userName = lj.UserName;
                if (string.IsNullOrEmpty(userName))
                {
                    userName = "Unidentified" + anuser; anuser++;
                }
                //мы должны собрать статистику
                foreach (var fileDatas in lj.DocsDateUse)
                {
                    //DateTime dateTime = fileDatas.Key;
                    DateTime dateTime = fileDatas.Key.Date; // ← приводим к дате без времени и Kind-зависимости
                    double dayPast = (DateTimeNow - dateTime).TotalDays;
                    if (dayPast > DaysBack || DaysFreshBack>0.1&& DaysFreshBack > dayPast)
                    {
                        continue;
                    }
                    
                    foreach (var fileData in fileDatas.Value)
                    {
                        string folderProject = fileData.Key;

                        if(ServerOnly && !folderProject.StartsWith(PrefixServer))
                        {
                            continue;
                        }
                        int useInt = fileData.Value;

                        if(!DictFileDataLook.TryGetValue(folderProject, out var data))
                        {
                            data = new FileDataLook(folderProject);
                            DictFileDataLook[folderProject]= data;
                           
                        }
                        data.AddData(userName, dateTime, useInt);

                    }
                }
            }

        }


        public Dictionary<string, FileDataLook> DictFileDataLook = new Dictionary<string, FileDataLook>();

        public void GetStatistics()
        {
            if(string.IsNullOrEmpty(FolderPathStatistics))
            {
                return;
            }

            //иначе тут собираем статистику с одной стороны по проектам, с другой - по пользователям
            int c = 0;
        }

    }

    public class FileDataLook
    {
        public string FolderProject;
        public string FileName;
        //даты когда пользователи пользовались проектом
        public Dictionary<DateTime, int> DatesProjectCountUsers = new Dictionary<DateTime, int>();
        public Dictionary<DateTime, int> DatesProjectCountClick = new Dictionary<DateTime, int>();
        
        //даты и пользователи которые пользовались проектом
        public Dictionary<DateTime, HashSet<string>> DatesUsers = new Dictionary<DateTime, HashSet<string>>();

        public HashSet<string> UserNames = new HashSet<string>();

        public FileDataLook(string folderProject)
        {
            FolderProject = folderProject;
            FileName = Path.GetFileNameWithoutExtension(FolderProject);//Path.GetFileName(FolderProject);
        }

       public void AddData(string userName, DateTime dateTime,int useInt)
        {
            DatesProjectCountUsers.TryGetValue(dateTime, out var value);
            DatesProjectCountUsers[dateTime] = value + 1;
            DatesProjectCountClick.TryGetValue(dateTime, out value);
            DatesProjectCountClick[dateTime] = value + useInt;

            UserNames.Add(userName);
            if(!DatesUsers.TryGetValue(dateTime, out var hu))
            {
                hu = new HashSet<string>();
                DatesUsers[dateTime] = hu;
            }
            hu.Add(userName);
        }
    }
}
