// Run with: dotnet-script TakePhase4Screenshot.cs
// Or build as a temp console app
using System;
using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;

var fxDir = AppContext.BaseDirectory;
var app = Application.Attach(System.Diagnostics.Process.GetProcessesByName("PetSalon.Wpf")[0]);
var auto = new UIA3Automation();
var win = app.GetMainWindow(auto, TimeSpan.FromSeconds(10));

// Helper
AutomationElement WaitFor(string id, int ms = 5000)
{
    var deadline = DateTime.UtcNow.AddMilliseconds(ms);
    while (DateTime.UtcNow < deadline)
    {
        var el = win.FindFirstDescendant(c => c.ByAutomationId(id));
        if (el is not null) return el;
        Thread.Sleep(75);
    }
    throw new TimeoutException($"'{id}' not found in {ms}ms");
}

void ClickById(string id) { WaitFor(id).AsButton().Invoke(); Thread.Sleep(200); }
void TypeInto(string id, string text) { WaitFor(id).AsTextBox().Text = text; Thread.Sleep(100); }

// Navigate to owners
ClickById("nav-owners");
Thread.Sleep(500);

// Create owner
ClickById("btn-new-owner");
TypeInto("txt-owner-name", "截圖示範飼主");
TypeInto("txt-owner-national-id", "A987654321");
TypeInto("txt-owner-phone", "0912999888");
TypeInto("txt-owner-address", "桃園市桃園區慈文路322巷5號");
ClickById("btn-save-owner");
Thread.Sleep(800);

// Close dialog
var dialogs = win.ModalWindows;
foreach (var d in dialogs)
{
    var ok = d.FindFirstDescendant(c => c.ByName("確定"));
    if (ok is not null) ok.AsButton().Invoke();
}
Thread.Sleep(400);

// Open pet form
ClickById("btn-add-pet");
Thread.Sleep(600);

// Select 異常 for eyes
var eyesCombo = win.FindFirstDescendant(c => c.ByAutomationId("cmb-phys-eyes"));
if (eyesCombo is not null)
{
    eyesCombo.AsComboBox().Select("異常");
    Thread.Sleep(400);
}

Console.WriteLine("Screenshot: Pet edit with Eyes=異常");
