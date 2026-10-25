using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows.Forms;

namespace Reinforcement
{
    /// <summary>
    /// Утилитный класс для глобального доступа к Revit API-объектам.
    /// Используется в качестве фасада к UIApplication, UIDocument и Document.
    /// </summary>
    public static class RevitAPI
    {
        public static LookUsers LookUsers = new LookUsers();//статистика пользователя по командам

        public static AppErrors AppErrors = new AppErrors();//ошибки пользователя

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

        private static UIApplication _uiApplication;

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

            _uiApplication = commandData.Application;
            RevitAPI.LookUsers.Update();
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
                var revitHandle = RevitAPI.UiApplication.MainWindowHandle;
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
    }
}

