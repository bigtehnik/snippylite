using System.Threading;
using System.Runtime.InteropServices;
using System.IO;

namespace FloatingTextPanel;

/// <summary>
/// Вставка текста через Clipboard + WM_PASTE в дочерний Edit-контроль.
/// Надёжно работает для Notepad и большинства окон с текстовыми полями.
/// </summary>
public static class TextInserter
{
    private const uint WM_PASTE = 0x0302;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static void InsertText(string text, IntPtr targetWindow)
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "insert_log.txt");
        void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        Log("=== InsertText START ===");
        Log($"text length={text.Length}, targetWindow={targetWindow}");

        if (string.IsNullOrEmpty(text))
            return;

        // 1. Активируем целевое окно
        bool activated = SetForegroundWindow(targetWindow);
        int err1 = Marshal.GetLastWin32Error();
        Log($"SetForegroundWindow => {activated}, err={err1}");
        Thread.Sleep(200); // даём время на установку фокуса

        // Проверим, стало ли окно foreground
        IntPtr fg = GetForegroundWindow();
        Log($"Current foreground: {fg} (expected {targetWindow})");

        // 2. Ищем дочерний текстовый контрол (Edit или RichEdit)
        IntPtr editCtrl = FindWindowEx(targetWindow, IntPtr.Zero, "Edit", null);
        if (editCtrl == IntPtr.Zero)
            editCtrl = FindWindowEx(targetWindow, IntPtr.Zero, "RichEdit20W", null);
        if (editCtrl == IntPtr.Zero)
            editCtrl = FindWindowEx(targetWindow, IntPtr.Zero, "RichEdit50W", null);

        Log($"Found edit control: {editCtrl}");

        if (editCtrl != IntPtr.Zero)
        {
            // 3. Копируем текст в буфер обмена
            try
            {
                Clipboard.SetText(text);
                Log("Text copied to clipboard");
            }
            catch (Exception ex)
            {
                Log($"Clipboard.SetText failed: {ex.Message}");
                // Пробуем fallback через SendInput (если есть)
                TryFallbackSendInput(targetWindow, text);
                return;
            }

            // 4. Отправляем WM_PASTE в edit control
            IntPtr result = SendMessage(editCtrl, WM_PASTE, IntPtr.Zero, IntPtr.Zero);
            Log($"SendMessage WM_PASTE returned {result}");
            // WM_PASTE обычно возвращает 0, но это не ошибка
            Log("Insert via WM_PASTE SUCCESS");
            return;
        }
        else
        {
            Log("Edit control not found, trying fallback with SendInput");
            TryFallbackSendInput(targetWindow, text);
        }
    }

    private static void TryFallbackSendInput(IntPtr targetWindow, string text)
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "insert_log.txt");
        void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        try
        {
            Clipboard.SetText(text);
            Log("Fallback: text copied to clipboard");

            // Активируем окно ещё раз
            SetForegroundWindow(targetWindow);
            Thread.Sleep(100);

            // Отправляем Ctrl+V через SendInput
            var inputs = new List<INPUT>();
            // Ctrl down
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = (ushort)Keys.ControlKey, wScan = 0, dwFlags = KEYEVENTF_KEYDOWN }
                }
            });
            // V down
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = (ushort)Keys.V, wScan = 0, dwFlags = KEYEVENTF_KEYDOWN }
                }
            });
            // V up
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = (ushort)Keys.V, wScan = 0, dwFlags = KEYEVENTF_KEYUP }
                }
            });
            // Ctrl up
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = (ushort)Keys.ControlKey, wScan = 0, dwFlags = KEYEVENTF_KEYUP }
                }
            });

            uint sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
            int err = Marshal.GetLastWin32Error();
            Log($"Fallback SendInput Ctrl+V: sent={sent}, expected={inputs.Count}, err={err}");
        }
        catch (Exception ex)
        {
            Log($"Fallback exception: {ex}");
        }
    }

    // structures for SendInput
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}