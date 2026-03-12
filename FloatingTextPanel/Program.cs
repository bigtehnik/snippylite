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
        ApplicationConfiguration.Initialize();
        Application.Run(new MainPanel());
    }
}