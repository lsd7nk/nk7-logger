using Microsoft.Extensions.Logging;

namespace Nk7.Logger
{
    public static class LoggerLocator
    {
        private static readonly LocatorLoggerService _instance = new();

        private static ILoggerService _target;
        private static ILoggerService _console;

        public static ILoggerService Instance => _instance;

#if UNITY_EDITOR
        internal static ILoggerService Target => _target;
#endif

        internal static ILoggerService Current => _target ?? (_console ??= CreateConsoleLogger());

        public static void Initialize(ILoggerService logger)
        {
            _target = ReferenceEquals(logger, _instance) ? _target : logger;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            _target = null;
            _console = null;
        }

        private static ILoggerService CreateConsoleLogger()
        {
            return new LoggerService(LoggerFactory.GetLogger(LogLevel.Trace));
        }
    }
}
