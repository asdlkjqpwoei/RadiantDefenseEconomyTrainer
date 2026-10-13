using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

class TrainerSelfTest
{
    static readonly long[] fakeGameObject = new long[32];
    static readonly long[] fakeModuleStorage = new long[1];
    static int failures;
    static int checks;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--fake-target")
            return RunFakeTarget();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            TestLocalizationAndForm();
            TestLocatorAndMoneyWrites();
        }
        catch (Exception ex)
        {
            Check(false, "Unexpected test exception: " + ex);
        }

        Console.WriteLine(failures == 0
            ? "ALL PASS (" + checks + " checks)"
            : failures + " of " + checks + " checks failed");
        return failures == 0 ? 0 : 1;
    }

    static int RunFakeTarget()
    {
        GCHandle gameHandle = default(GCHandle);
        GCHandle moduleHandle = default(GCHandle);
        try
        {
            gameHandle = GCHandle.Alloc(fakeGameObject, GCHandleType.Pinned);
            moduleHandle = GCHandle.Alloc(fakeModuleStorage, GCHandleType.Pinned);
            long gameObjectAddress = gameHandle.AddrOfPinnedObject().ToInt64();
            long moduleBase = moduleHandle.AddrOfPinnedObject().ToInt64();

            fakeGameObject[0] = moduleBase + RadiantDefenseMoneyLocator.GAME_VTABLE_RVA;
            uint encodedMoney = unchecked((uint)RadiantDefenseMoneyLocator.EncodeMoney(500));
            fakeGameObject[19] = (long)encodedMoney << 32;

            Console.WriteLine("MODULE_BASE_ADDR=" + moduleBase.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("GAME_OBJECT_ADDR=" + gameObjectAddress.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("READY");
            Console.Out.Flush();
            Console.ReadLine();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            if (gameHandle.IsAllocated) gameHandle.Free();
            if (moduleHandle.IsAllocated) moduleHandle.Free();
        }
    }

    static void TestLocalizationAndForm()
    {
        TrainerLocalization localization = TrainerLocalization.LoadDefault();
        Check(localization.LanguageCode == localization.DefaultLanguage, "YAML default language is loaded");
        Check(!string.IsNullOrWhiteSpace(localization.Get("zh-CN", "ui.current_money")),
            "Chinese text comes from YAML");
        Check(!string.IsNullOrWhiteSpace(localization.Get("en", "ui.current_money")),
            "English text comes from YAML");
        Check(!string.IsNullOrWhiteSpace(localization.Get("zh-CN", "ui.relocating")) &&
            !string.IsNullOrWhiteSpace(localization.Get("en", "ui.relocating")),
            "Balance-read recovery state is localized in both languages");
        LocalizedMessage locatorError = new LocalizedMessage("locator.version_unsupported", "1D8000");
        localization.SetLanguage("zh-CN");
        string chineseError = localization.Format("status.locator_failed", locatorError);
        localization.SetLanguage("en");
        string englishError = localization.Format("status.locator_failed", locatorError);
        Check(chineseError != englishError, "Nested status and locator errors follow the selected language");
        localization.SetLanguage(localization.DefaultLanguage);

        using (TrainerForm form = new TrainerForm(localization))
        {
            Check(form.Text == localization.Get("app.title"), "Window title is localized");
            Check(form.FormBorderStyle == FormBorderStyle.FixedSingle &&
                !form.MaximizeBox && form.MinimizeBox, "Window keeps the requested fixed/minimize behavior");
            Check(form.StartPosition == FormStartPosition.CenterScreen, "Window opens centered");
            Check(!ContainsTabs(form), "The trainer has no tabbed UI");

            TextBox instructions = FindControl(form, "instructionsLog") as TextBox;
            Check(instructions != null &&
                instructions.Text.StartsWith(localization.Get("ui.instructions"), StringComparison.Ordinal),
                "Instructions are loaded from YAML");

            Label moneyCaption = FindControl(form, "currentMoneyCaption") as Label;
            Check(moneyCaption != null && moneyCaption.Text == localization.Get("ui.current_money"),
                "The current-balance label is localized");

            ComboBox languageSelector = FindControl(form, "languageSelector") as ComboBox;
            Check(languageSelector != null && languageSelector.Items.Count == 2,
                "Both configured languages are available");
            if (languageSelector != null)
            {
                languageSelector.SelectedIndex = 1;
                Application.DoEvents();
                Check(moneyCaption != null &&
                    moneyCaption.Text == localization.Get("en", "ui.current_money"),
                    "English selection updates the interface text");
                Check(instructions != null &&
                    instructions.Text.StartsWith(localization.Get("en", "ui.instructions"), StringComparison.Ordinal),
                    "Switching languages updates instructions");

                languageSelector.SelectedIndex = 0;
                Application.DoEvents();
                Check(moneyCaption != null && moneyCaption.Text == localization.Get("ui.current_money"),
                    "Switching back restores Chinese interface text");
                Check(instructions != null &&
                    instructions.Text.StartsWith(localization.Get("ui.instructions"), StringComparison.Ordinal),
                    "Switching back restores Chinese text");
            }

            Size originalSize = form.ClientSize;
            form.Show();
            Application.DoEvents();
            Check(form.ClientSize == originalSize, "Showing the form preserves its fixed layout");
            form.Hide();
        }
    }

    static void TestLocatorAndMoneyWrites()
    {
        Process target = new Process();
        target.StartInfo = new ProcessStartInfo {
            FileName = Assembly.GetExecutingAssembly().Location,
            Arguments = "--fake-target",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        long moduleBase = 0;
        long expectedGameObject = 0;
        bool moduleAddressRead = false;
        bool gameAddressRead = false;
        IntPtr handle = IntPtr.Zero;
        using (AutoResetEvent targetReady = new AutoResetEvent(false))
        {
            target.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                const string modulePrefix = "MODULE_BASE_ADDR=";
                const string objectPrefix = "GAME_OBJECT_ADDR=";
                if (e.Data.StartsWith(modulePrefix, StringComparison.Ordinal))
                    moduleAddressRead = long.TryParse(e.Data.Substring(modulePrefix.Length),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out moduleBase);
                else if (e.Data.StartsWith(objectPrefix, StringComparison.Ordinal))
                    gameAddressRead = long.TryParse(e.Data.Substring(objectPrefix.Length),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out expectedGameObject);
                else if (e.Data == "READY")
                    targetReady.Set();
            };

            try
            {
                Check(target.Start(), "Fake target process starts");
                target.BeginOutputReadLine();
                Check(targetReady.WaitOne(TimeSpan.FromSeconds(10)), "Fake target initializes its game object");
                Check(moduleAddressRead && gameAddressRead, "Fake target exposes its test addresses");
                if (!moduleAddressRead || !gameAddressRead) return;

                handle = MemApi.OpenTarget(target.Id);
                Check(handle != IntPtr.Zero, "OpenProcess can access the fake target");
                if (handle == IntPtr.Zero) return;

                Check(RadiantDefenseMoneyLocator.DecodeMoney(RadiantDefenseMoneyLocator.EncodeMoney(30)) == 30 &&
                    RadiantDefenseMoneyLocator.DecodeMoney(RadiantDefenseMoneyLocator.EncodeMoney(123456)) == 123456,
                    "Balance XOR encoding round-trips");

                List<GameModuleInfo> modules = new List<GameModuleInfo> {
                    new GameModuleInfo {
                        Name = RadiantDefenseMoneyLocator.GAME_MODULE,
                        Base = moduleBase,
                        Size = RadiantDefenseMoneyLocator.EXPECTED_MODULE_SIZE
                    }
                };
                long gameObject, moneyAddress;
                int money;
                LocalizedMessage error;
                bool located = RadiantDefenseMoneyLocator.TryLocate(handle, modules,
                    out gameObject, out moneyAddress, out money, out error);
                Check(located && gameObject == expectedGameObject &&
                    moneyAddress == expectedGameObject + RadiantDefenseMoneyLocator.MONEY_FIELD_OFFSET &&
                    money == 500, "Game object and initial balance are located");
                if (!located) return;

                int writeError, restoreError, actual;
                bool mismatch;
                bool wrote = RadiantDefenseMoneyLocator.TryWriteMoney(handle, moneyAddress, 123456,
                    out writeError, out restoreError, out mismatch);
                Check(wrote, "Setting money writes and verifies the encoded balance");
                Check(RadiantDefenseMoneyLocator.TryReadMoney(handle, moneyAddress, out actual, out writeError) &&
                    actual == 123456, "Set balance reads back correctly");

                long added = (long)actual + 1234;
                bool addedSuccessfully = added <= int.MaxValue &&
                    RadiantDefenseMoneyLocator.TryWriteMoney(handle, moneyAddress, (int)added,
                        out writeError, out restoreError, out mismatch);
                Check(addedSuccessfully, "Adding money writes the expected balance");
                Check(RadiantDefenseMoneyLocator.TryReadMoney(handle, moneyAddress, out actual, out writeError) &&
                    actual == 124690, "Added balance reads back correctly");

                bool locked = true;
                for (int i = 0; i < 3; i++)
                {
                    Thread.Sleep(100);
                    if (!RadiantDefenseMoneyLocator.TryWriteMoney(handle, moneyAddress, 555555,
                        out writeError, out restoreError, out mismatch))
                        locked = false;
                }
                Check(locked &&
                    RadiantDefenseMoneyLocator.TryReadMoney(handle, moneyAddress, out actual, out writeError) &&
                    actual == 555555, "Repeated writes maintain the locked balance");

                modules[0].Size++;
                bool rejected = !RadiantDefenseMoneyLocator.TryLocate(handle, modules,
                    out gameObject, out moneyAddress, out money, out error);
                Check(rejected && error != null && error.Key == "locator.version_unsupported",
                    "Unsupported game versions are rejected with a localization key");

                TestGameExitWhileTrainerRemainsOpen(target, handle, moneyAddress);
            }
            finally
            {
                if (handle != IntPtr.Zero) MemApi.CloseHandle(handle);
                if (!target.HasExited)
                {
                    try
                    {
                        target.StandardInput.WriteLine("stop");
                        target.StandardInput.Flush();
                    }
                    catch (InvalidOperationException) { }
                    if (!target.WaitForExit(3000))
                        target.Kill();
                }
                target.Dispose();
            }
        }
    }

    static void TestGameExitWhileTrainerRemainsOpen(Process target, IntPtr handle, long moneyAddress)
    {
        TrainerLocalization localization = TrainerLocalization.LoadDefault();
        using (TrainerForm form = new TrainerForm(localization))
        {
            target.StandardInput.WriteLine("exit");
            target.StandardInput.Flush();
            bool exited = target.WaitForExit(3000);
            Check(exited, "Fake game process exits while the trainer stays open");
            if (!exited) return;

            int balance, readError;
            bool readSucceeded = RadiantDefenseMoneyLocator.TryReadMoney(handle,
                moneyAddress, out balance, out readError);
            Check(!readSucceeded, "Balance read fails after the game process exits");
            if (readSucceeded) readError = 6;
            form.HandleBalanceReadFailure(readError);
            Label status = FindControl(form, "moneyLocationStatusLabel") as Label;
            Check(status != null && status.Text == localization.Get("ui.relocating"),
                "A failed read after game exit enters the localized recovery state without crashing" +
                " (actual: " + (status == null ? "<missing>" : status.Text) + ")");
        }
    }

    static Control FindControl(Control parent, string name)
    {
        foreach (Control child in parent.Controls)
        {
            if (child.Name == name) return child;
            Control nested = FindControl(child, name);
            if (nested != null) return nested;
        }
        return null;
    }

    static bool ContainsTabs(Control control)
    {
        if (control is TabControl) return true;
        foreach (Control child in control.Controls)
            if (ContainsTabs(child)) return true;
        return false;
    }

    static void Check(bool passed, string message)
    {
        checks++;
        Console.WriteLine((passed ? "PASS " : "FAIL ") + message);
        if (!passed) failures++;
    }
}
