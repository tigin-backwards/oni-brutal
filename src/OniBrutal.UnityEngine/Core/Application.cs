using System;
using System.Diagnostics;
using System.Globalization;
using OniBrutal.Engine;

namespace UnityEngine
{
    public partial class Application
    {
        public static string dataPath => EngineConfig.DataPath;
        public static string streamingAssetsPath => EngineConfig.StreamingAssetsPath;
        public static string persistentDataPath => EngineConfig.PersistentDataPath;
        public static string temporaryCachePath => EngineConfig.TemporaryCachePath;
        public static string consoleLogPath => EngineConfig.ConsoleLogPath;
        public static string companyName => EngineConfig.CompanyName;
        public static string productName => EngineConfig.ProductName;
        public static string version => EngineConfig.Version;
        public static string unityVersion => EngineConfig.UnityVersion;

        /// The player of the operating system the engine runs on.
        public static RuntimePlatform platform => OperatingSystem.IsWindows() ? RuntimePlatform.WindowsPlayer : RuntimePlatform.LinuxPlayer;
        public static bool isEditor => false;
        public static bool isPlaying => true;
        public static bool isBatchMode => EngineConfig.IsBatchMode;
        public static bool isMobilePlatform => false;
        public static bool isConsolePlatform => false;
        public static bool isFocused => PlayerLoop.HasFocus;
        public static bool runInBackground { get; set; } = true;

        public static int targetFrameRate
        {
            get => PlayerLoop.TargetFrameRate;
            set => PlayerLoop.TargetFrameRate = value;
        }

        /// Unity's players derive the language from the user's UI language (Windows) or the
        /// locale environment (Linux), which the current UI culture reflects.
        public static SystemLanguage systemLanguage => Languages.FromCulture(CultureInfo.CurrentUICulture);

        /// Quits at the end of the frame, after OnApplicationQuit, with this exit code.
        public static void Quit(int exitCode)
        {
            EngineLog.Engine($"Application.Quit({exitCode}) requested");
            PlayerLoop.RequestQuit(exitCode);
        }

        public static void Quit() => Quit(0);

        /// The Linux player hands URLs to the desktop's opener.
        public static void OpenURL(string url)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("URL must not be null or empty.", nameof(url));
            try
            {
                Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to open URL " + url + ": " + e.Message);
            }
        }

        public static event LogCallback logMessageReceived;
        public static event LogCallback logMessageReceivedThreaded;

        internal static void RaiseLogMessage(string condition, string stackTrace, LogType type)
        {
            var handler = logMessageReceived;
            if (handler == null) return;
            try
            {
                handler(condition, stackTrace, type);
            }
            catch (Exception e)
            {
                // A throwing log callback must not recurse into logging through itself.
                Console.Error.WriteLine("Exception in Application.logMessageReceived handler: " + e);
            }
        }

        internal static void RaiseLogMessageThreaded(string condition, string stackTrace, LogType type)
        {
            var handler = logMessageReceivedThreaded;
            if (handler == null) return;
            try
            {
                handler(condition, stackTrace, type);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Exception in Application.logMessageReceivedThreaded handler: " + e);
            }
        }
    }

    public partial class AsyncOperation
    {
        bool m_IsDone;
        float m_Progress;
        Action<AsyncOperation> m_completeCallback;

        public AsyncOperation() { }

        public bool isDone => m_IsDone;
        public float progress => m_IsDone ? 1f : m_Progress;
        public int priority { get; set; }
        public bool allowSceneActivation { get; set; } = true;

        /// Subscribing to an operation that has already completed invokes the callback at once.
        public event Action<AsyncOperation> completed
        {
            add
            {
                if (m_IsDone) value(this);
                else m_completeCallback += value;
            }
            remove => m_completeCallback -= value;
        }

        internal void SetProgress(float value) => m_Progress = value;

        /// Marks the operation done and invokes completion callbacks (main thread).
        internal void Complete()
        {
            if (m_IsDone) return;
            m_IsDone = true;
            var callback = m_completeCallback;
            m_completeCallback = null;
            if (callback == null) return;
            try
            {
                callback(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}

namespace OniBrutal.Engine
{
    /// Process-wide settings the host establishes before any game code runs.
    internal static class EngineConfig
    {
        public static string DataPath = "";
        public static string StreamingAssetsPath = "";
        public static string PersistentDataPath = "";
        public static string TemporaryCachePath = "";
        public static string ConsoleLogPath = "";
        public static string CompanyName = "";
        public static string ProductName = "";
        public static string Version = "";
        public static string UnityVersion = "";
        public static bool IsBatchMode;
        public static bool Headless;
    }

    internal static class Languages
    {
        public static UnityEngine.SystemLanguage FromCulture(CultureInfo culture)
        {
            var name = culture.Name;
            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return name.Contains("TW", StringComparison.OrdinalIgnoreCase) || name.Contains("HK", StringComparison.OrdinalIgnoreCase) || name.Contains("Hant", StringComparison.OrdinalIgnoreCase)
                    ? UnityEngine.SystemLanguage.ChineseTraditional
                    : UnityEngine.SystemLanguage.ChineseSimplified;
            return culture.TwoLetterISOLanguageName switch
            {
                "af" => UnityEngine.SystemLanguage.Afrikaans,
                "ar" => UnityEngine.SystemLanguage.Arabic,
                "eu" => UnityEngine.SystemLanguage.Basque,
                "be" => UnityEngine.SystemLanguage.Belarusian,
                "bg" => UnityEngine.SystemLanguage.Bulgarian,
                "ca" => UnityEngine.SystemLanguage.Catalan,
                "cs" => UnityEngine.SystemLanguage.Czech,
                "da" => UnityEngine.SystemLanguage.Danish,
                "nl" => UnityEngine.SystemLanguage.Dutch,
                "en" => UnityEngine.SystemLanguage.English,
                "et" => UnityEngine.SystemLanguage.Estonian,
                "fo" => UnityEngine.SystemLanguage.Faroese,
                "fi" => UnityEngine.SystemLanguage.Finnish,
                "fr" => UnityEngine.SystemLanguage.French,
                "de" => UnityEngine.SystemLanguage.German,
                "el" => UnityEngine.SystemLanguage.Greek,
                "he" => UnityEngine.SystemLanguage.Hebrew,
                "hu" => UnityEngine.SystemLanguage.Hungarian,
                "is" => UnityEngine.SystemLanguage.Icelandic,
                "id" => UnityEngine.SystemLanguage.Indonesian,
                "it" => UnityEngine.SystemLanguage.Italian,
                "ja" => UnityEngine.SystemLanguage.Japanese,
                "ko" => UnityEngine.SystemLanguage.Korean,
                "lv" => UnityEngine.SystemLanguage.Latvian,
                "lt" => UnityEngine.SystemLanguage.Lithuanian,
                "nb" or "nn" or "no" => UnityEngine.SystemLanguage.Norwegian,
                "pl" => UnityEngine.SystemLanguage.Polish,
                "pt" => UnityEngine.SystemLanguage.Portuguese,
                "ro" => UnityEngine.SystemLanguage.Romanian,
                "ru" => UnityEngine.SystemLanguage.Russian,
                "sr" or "hr" or "bs" => UnityEngine.SystemLanguage.SerboCroatian,
                "sk" => UnityEngine.SystemLanguage.Slovak,
                "sl" => UnityEngine.SystemLanguage.Slovenian,
                "es" => UnityEngine.SystemLanguage.Spanish,
                "sv" => UnityEngine.SystemLanguage.Swedish,
                "th" => UnityEngine.SystemLanguage.Thai,
                "tr" => UnityEngine.SystemLanguage.Turkish,
                "uk" => UnityEngine.SystemLanguage.Ukrainian,
                "vi" => UnityEngine.SystemLanguage.Vietnamese,
                "hi" => UnityEngine.SystemLanguage.Hindi,
                _ => UnityEngine.SystemLanguage.Unknown,
            };
        }
    }
}
