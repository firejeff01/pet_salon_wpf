using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;

namespace PetSalon.Wpf.UiTests.Helpers;

public static class UiTestExtensions
{
    public static void ClickById(this AppFixture fx, string id)
    {
        fx.WaitForId(id).AsButton().Invoke();
        Thread.Sleep(150);
    }

    public static void TypeInto(this AppFixture fx, string textBoxId, string text)
    {
        var tb = fx.WaitForId(textBoxId).AsTextBox();
        tb.Text = text;
        Thread.Sleep(60);
    }

    public static string ReadText(this AppFixture fx, string id)
    {
        var el = fx.WaitForId(id);
        return el.AsTextBox().Text ?? string.Empty;
    }

    public static bool? IsEnabled(this AppFixture fx, string id)
        => fx.WaitForId(id).IsEnabled;

    public static AutomationElement? FindAnyContaining(this AppFixture fx, string fragment)
    {
        return fx.MainWindow.FindAllDescendants()
            .FirstOrDefault(e => (e.Properties.Name.ValueOrDefault ?? string.Empty).Contains(fragment));
    }

    public static AutomationElement? FindAnyContaining(this AutomationElement root, string fragment)
    {
        return root.FindAllDescendants()
            .FirstOrDefault(e => (e.Properties.Name.ValueOrDefault ?? string.Empty).Contains(fragment));
    }

    /// <summary>從整個桌面找屬於本 process 的視窗（包含 WindowStyle=None 的 MessageDialog）。</summary>
    private static IEnumerable<AutomationElement> OwnedProcessWindows(AppFixture fx)
    {
        var pid = fx.App.ProcessId;
        try
        {
            return fx.Automation.GetDesktop()
                .FindAllChildren()
                .Where(c => c.Properties.ProcessId.ValueOrDefault == pid);
        }
        catch
        {
            return Array.Empty<AutomationElement>();
        }
    }

    public static Window? FindDialog(this AppFixture fx, string title, int timeoutMs = 6000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var modal = fx.MainWindow.ModalWindows.FirstOrDefault(w =>
                (w.Title ?? string.Empty).Contains(title) ||
                w.FindAnyContaining(title) is not null);
            if (modal is not null) return modal;

            foreach (var c in OwnedProcessWindows(fx))
            {
                var name = c.Properties.Name.ValueOrDefault ?? string.Empty;
                if (name.Contains(title) || c.FindAnyContaining(title) is not null)
                    return c.AsWindow();
            }
            Thread.Sleep(120);
        }
        return null;
    }

    /// <summary>關掉 process 內所有按了「確定」就會關的對話框（含 WindowStyle=None 的 MessageDialog）。</summary>
    public static void CloseAllDialogs(this AppFixture fx)
    {
        Thread.Sleep(300);  // 讓對話框先完成 layout
        var pid = fx.App.ProcessId;
        try
        {
            // 在整個桌面掃，找名稱為「確定」且屬於本 process 的所有 Button
            var desktop = fx.Automation.GetDesktop();
            var allOk = desktop.FindAllDescendants(c =>
                c.ByName("確定").And(c.ByControlType(ControlType.Button)));
            foreach (var ok in allOk)
            {
                try
                {
                    if (ok.Properties.ProcessId.ValueOrDefault == pid)
                        ok.AsButton().Invoke();
                }
                catch { }
            }
        }
        catch { }
        Thread.Sleep(300);
    }
}
