using System;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    /// <summary>
    /// Фоновый синхронизатор вида-источника с видом-целью.
    /// ВАЖНО: Revit API не потокобезопасен. Execute() вызывается
    /// только на главном потоке через ExternalEvent.
    /// </summary>
    public static class ViewPositionTime
    {

        //ViewPositionTime.Initialize(); - при старте
        //_timer?.Dispose();
        //ViewPositionTime.Shutdown();

        private static ExternalEvent _externalEvent;
        private static SyncHandler _handler;
        private static readonly object _initLock = new object();

        /// <summary>
        /// Вызвать ОДИН раз при инициализации приложения (OnStartup).
        /// Повторные вызовы безопасны — ничего не делают.
        /// </summary>
        public static void Initialize()
        {
            lock (_initLock)
            {
                if (_externalEvent != null) return;
                _handler = new SyncHandler();
                _externalEvent = ExternalEvent.Create(_handler);
            }
        }

        /// <summary>
        /// Вызывать из таймера раз в N секунд. Можно с любого потока —
        /// Raise() лишь планирует выполнение Execute() на главном потоке.
        /// </summary>
        public static void RaiseTick()
        {
            if (_externalEvent == null) return;
            if (!ViewPositionBuffer.IsValid) return;
            if (!ViewPositionBuffer.HasTarget) return;
            _externalEvent.Raise();
        }

        /// <summary>Сброс — например, при закрытии приложения.</summary>
        public static void Shutdown()
        {
            lock (_initLock)
            {
                _externalEvent?.Dispose();
                _externalEvent = null;
                _handler = null;
            }
        }

        // Выполняется Revit'ом на главном потоке
        private sealed class SyncHandler : IExternalEventHandler
        {
            public void Execute(UIApplication app)
            {
                if (!ViewPositionOperations.TrySyncFromBuffer(out string error))
                {
                    //AppErrors.LogError(new Exception(error));
                }
            }

            public string GetName() => "ViewPositionSync";
        }


        public static int timeSecond = 5;
        public static readonly System.Threading.Timer _timer = new System.Threading.Timer(
        _ => ViewPositionTime.RaiseTick(),
        null,
        dueTime: TimeSpan.FromSeconds(timeSecond),
        period: TimeSpan.FromSeconds(timeSecond));
        }
}