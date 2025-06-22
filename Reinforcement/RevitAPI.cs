using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    /// <summary>
    /// Утилитный класс для глобального доступа к Revit API-объектам.
    /// Используется в качестве фасада к UIApplication, UIDocument и Document.
    /// </summary>
    public static class RevitAPI
    {
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
        public static UIDocument UiDocument => UiApplication.ActiveUIDocument;

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
    }
}

