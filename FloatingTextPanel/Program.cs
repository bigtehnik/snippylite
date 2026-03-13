using System.Globalization;

namespace FloatingTextPanel;

/// <summary>
/// Точка входа приложения.
/// </summary>
internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // 🔥 ВАЖНО: отключаем DPI‑масштабирование,
        // иначе окно не может быть меньше ~20×40
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainPanel());
    }
}
