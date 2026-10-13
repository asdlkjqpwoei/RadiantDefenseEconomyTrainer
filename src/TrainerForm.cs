using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public class TrainerForm : Form
{
    readonly TrainerLocalization localization;

    TextBox txtLog = new TextBox();
    StatusStrip statusStrip = new StatusStrip();
    ToolStripStatusLabel lblStatus = new ToolStripStatusLabel();
    System.Windows.Forms.Timer autoTimer = new System.Windows.Forms.Timer();

    IntPtr gameHandle = IntPtr.Zero;
    long gameMoneyAddress;
    bool locatingGameMoney;
    int nextMoneyLocateTick;
    int tickCount;
    int gamePid = -1;
    bool autoConnected;
    bool lockWriteWarningShown;
    bool applyingLanguage;
    bool hasBalance;
    int displayedBalance;
    string lastConnectionErrorSignature;
    string lastLoggedLocateErrorSignature;

    LocalizedMessage statusMessage;
    LocalizedMessage connectionMessage;
    LocalizedMessage moneyStatusMessage;
    LocalizedMessage moneyValueMessage;
    LocalizedMessage moneyLocateError;

    Label lblGame = new Label();
    Label lblLanguage = new Label();
    Label lblCurrent = new Label();
    Label lblAdd = new Label();
    Label lblHint = new Label();
    Label lblHelp = new Label();
    Label lblConn = new Label();
    Label lblMoneyValue = new Label();
    Label lblMoneyStatus = new Label();
    GroupBox gbLocate = new GroupBox();
    TextBox txtSet = new TextBox();
    TextBox txtAdd = new TextBox();
    Button btnSet = new Button();
    Button btnAdd = new Button();
    Button btnRelocate = new Button();
    Button btnRefreshProc = new Button();
    CheckBox chkLock = new CheckBox();
    ComboBox cbLanguage = new ComboBox();

    sealed class LanguageOption
    {
        public readonly string Code;
        public readonly string DisplayName;

        public LanguageOption(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public override string ToString() { return DisplayName; }
    }

    public TrainerForm() : this(TrainerLocalization.LoadDefault()) { }

    public TrainerForm(TrainerLocalization text)
    {
        if (text == null) throw new ArgumentNullException("text");
        localization = text;
        statusMessage = new LocalizedMessage("status.ready");
        connectionMessage = new LocalizedMessage("ui.game_not_found");
        moneyStatusMessage = new LocalizedMessage("ui.waiting_for_game");
        moneyValueMessage = new LocalizedMessage("ui.money_unavailable");

        BuildUi();
        ApplyLanguage();
        autoTimer.Interval = 500;
        autoTimer.Tick += AutoTick;
        autoTimer.Start();
    }

    void BuildUi()
    {
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        SizeGripStyle = SizeGripStyle.Hide;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);
        ClientSize = new Size(920, 580);

        Panel mainPanel = new Panel();
        mainPanel.Location = new Point(8, 8);
        mainPanel.Size = new Size(904, 270);
        mainPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        lblGame.Location = new Point(16, 18);
        lblGame.AutoSize = true;

        lblConn.Name = "connectionStatusLabel";
        lblConn.Location = new Point(100, 18);
        lblConn.Size = new Size(175, 22);
        lblConn.AutoEllipsis = true;

        btnRefreshProc.Location = new Point(280, 14);
        btnRefreshProc.Width = 90;
        btnRefreshProc.Click += delegate { DetectGame(); };

        lblLanguage.Location = new Point(650, 18);
        lblLanguage.AutoSize = true;

        cbLanguage.Name = "languageSelector";
        cbLanguage.Location = new Point(720, 14);
        cbLanguage.Width = 160;
        cbLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
        cbLanguage.SelectedIndexChanged += LanguageSelectionChanged;

        lblCurrent.Name = "currentMoneyCaption";
        lblCurrent.Location = new Point(16, 56);
        lblCurrent.AutoSize = true;

        lblMoneyValue.Name = "currentMoneyLabel";
        lblMoneyValue.Location = new Point(100, 56);
        lblMoneyValue.AutoSize = true;
        lblMoneyValue.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
        lblMoneyValue.ForeColor = Color.DarkGreen;

        txtSet.Location = new Point(200, 52);
        txtSet.Width = 110;
        txtSet.Text = localization.DefaultSetAmount.ToString(CultureInfo.InvariantCulture);

        btnSet.Location = new Point(320, 51);
        btnSet.Width = 90;
        btnSet.Click += delegate { AutoSet(); };

        chkLock.Location = new Point(420, 55);
        chkLock.AutoSize = true;

        btnRelocate.Location = new Point(510, 51);
        btnRelocate.Width = 90;
        btnRelocate.Click += delegate { StartRelocate(); };

        lblAdd.Location = new Point(610, 56);
        lblAdd.AutoSize = true;

        txtAdd.Location = new Point(650, 52);
        txtAdd.Width = 85;
        txtAdd.Text = localization.DefaultAddAmount.ToString(CultureInfo.InvariantCulture);

        btnAdd.Location = new Point(742, 51);
        btnAdd.Width = 80;
        btnAdd.Click += delegate { AutoAdd(); };

        gbLocate.Location = new Point(14, 92);
        gbLocate.Size = new Size(870, 120);

        lblHelp.Location = new Point(16, 24);
        lblHelp.Size = new Size(840, 42);

        lblMoneyStatus.Name = "moneyLocationStatusLabel";
        lblMoneyStatus.Location = new Point(16, 83);
        lblMoneyStatus.AutoSize = true;
        gbLocate.Controls.AddRange(new Control[] { lblHelp, lblMoneyStatus });

        lblHint.Location = new Point(16, 222);
        lblHint.AutoSize = true;
        lblHint.ForeColor = Color.Gray;

        mainPanel.Controls.AddRange(new Control[] {
            lblGame, lblConn, btnRefreshProc, lblLanguage, cbLanguage,
            lblCurrent, lblMoneyValue, txtSet, btnSet, chkLock, btnRelocate,
            lblAdd, txtAdd, btnAdd, gbLocate, lblHint
        });
        Controls.Add(mainPanel);

        txtLog.Name = "instructionsLog";
        txtLog.Location = new Point(8, 288);
        txtLog.Size = new Size(904, 268);
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.BackColor = SystemColors.Window;
        Controls.Add(txtLog);

        statusStrip.Items.Add(lblStatus);
        Controls.Add(statusStrip);
    }

    void ApplyLanguage()
    {
        Text = localization.Get("app.title");
        lblGame.Text = localization.Get("ui.game_status");
        lblLanguage.Text = localization.Get("ui.language_label");
        btnRefreshProc.Text = localization.Get("ui.detect");
        lblCurrent.Text = localization.Get("ui.current_money");
        btnSet.Text = localization.Get("ui.set");
        chkLock.Text = localization.Get("ui.lock");
        btnRelocate.Text = localization.Get("ui.relocate");
        lblAdd.Text = localization.Get("ui.add_label");
        btnAdd.Text = localization.Get("ui.add");
        gbLocate.Text = localization.Get("ui.locate_group");
        lblHelp.Text = localization.Get("ui.locate_help");
        lblHint.Text = localization.Get("ui.hint");
        txtLog.Text = localization.Get("ui.instructions") + Environment.NewLine +
            localization.Get("ui.operation_steps");

        applyingLanguage = true;
        try
        {
            cbLanguage.BeginUpdate();
            cbLanguage.Items.Clear();
            foreach (string code in new string[] { "zh-CN", "en" })
                cbLanguage.Items.Add(new LanguageOption(code, localization.Get(code, "language.name." + code)));

            for (int i = 0; i < cbLanguage.Items.Count; i++)
            {
                LanguageOption option = (LanguageOption)cbLanguage.Items[i];
                if (option.Code == localization.LanguageCode)
                {
                    cbLanguage.SelectedIndex = i;
                    break;
                }
            }
        }
        finally
        {
            cbLanguage.EndUpdate();
            applyingLanguage = false;
        }

        lblConn.Text = localization.Format(connectionMessage);
        lblMoneyStatus.Text = localization.Format(moneyStatusMessage);
        lblStatus.Text = localization.Format(statusMessage);
        lblMoneyValue.Text = hasBalance
            ? displayedBalance.ToString(CultureInfo.InvariantCulture)
            : localization.Format(moneyValueMessage);
    }

    void LanguageSelectionChanged(object sender, EventArgs e)
    {
        if (applyingLanguage) return;
        LanguageOption option = cbLanguage.SelectedItem as LanguageOption;
        if (option == null || option.Code == localization.LanguageCode) return;
        localization.SetLanguage(option.Code);
        ApplyLanguage();
    }

    void SetStatus(string key, params object[] arguments)
    {
        statusMessage = new LocalizedMessage(key, arguments);
        lblStatus.Text = localization.Format(statusMessage);
    }

    void SetConnectionMessage(string key, params object[] arguments)
    {
        connectionMessage = new LocalizedMessage(key, arguments);
        lblConn.Text = localization.Format(connectionMessage);
    }

    void SetMoneyStatus(string key, params object[] arguments)
    {
        moneyStatusMessage = new LocalizedMessage(key, arguments);
        lblMoneyStatus.Text = localization.Format(moneyStatusMessage);
    }

    void SetMoneyValueMessage(string key)
    {
        hasBalance = false;
        moneyValueMessage = new LocalizedMessage(key);
        lblMoneyValue.Text = localization.Format(moneyValueMessage);
    }

    void SetMoneyValue(int value)
    {
        hasBalance = true;
        displayedBalance = value;
        lblMoneyValue.Text = value.ToString(CultureInfo.InvariantCulture);
    }

    void DetectGame()
    {
        Process[] processes;
        try { processes = Process.GetProcessesByName("RadiantDefense"); }
        catch (Exception ex)
        {
            SetConnectionMessage("ui.process_detection_failed");
            SetStatus("status.process_detection_failed", ex.Message);
            return;
        }

        if (processes.Length == 0)
        {
            if (gameHandle != IntPtr.Zero)
            {
                MemApi.CloseHandle(gameHandle);
                gameHandle = IntPtr.Zero;
            }
            gamePid = -1;
            autoConnected = false;
            gameMoneyAddress = 0;
            locatingGameMoney = false;
            nextMoneyLocateTick = 0;
            moneyLocateError = null;
            lastLoggedLocateErrorSignature = null;
            lastConnectionErrorSignature = null;
            SetConnectionMessage("ui.game_not_found");
            SetMoneyStatus("ui.waiting_for_game");
            SetMoneyValueMessage("ui.money_unavailable");
            SetStatus("status.ready");
            return;
        }

        int pid = processes[0].Id;
        foreach (Process process in processes) process.Dispose();

        if (pid != gamePid || !autoConnected)
        {
            if (gameHandle != IntPtr.Zero)
            {
                MemApi.CloseHandle(gameHandle);
                gameHandle = IntPtr.Zero;
            }

            gameMoneyAddress = 0;
            locatingGameMoney = false;
            moneyLocateError = null;
            lastLoggedLocateErrorSignature = null;
            nextMoneyLocateTick = 0;
            gameHandle = MemApi.OpenTarget(pid);
            if (gameHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                LocalizedMessage connectionError = new LocalizedMessage("status.open_process_failed", error);
                SetConnectionMessage("ui.admin_required");
                SetMoneyStatus("status.open_process_failed", error);
                gamePid = pid;
                autoConnected = false;
                if (!string.Equals(lastConnectionErrorSignature, connectionError.Signature, StringComparison.Ordinal))
                {
                    SetStatus(connectionError.Key, connectionError.Arguments);
                    lastConnectionErrorSignature = connectionError.Signature;
                }
                return;
            }

            gamePid = pid;
            autoConnected = true;
            lastConnectionErrorSignature = null;
            SetConnectionMessage("ui.connected", pid);
            SetMoneyStatus("ui.locating");
            SetStatus("status.connected", pid);
            BeginMoneyLocate();
        }

        ResolveAndShow();
    }

    void ResolveAndShow()
    {
        if (!autoConnected || gameHandle == IntPtr.Zero) return;
        if (gameMoneyAddress == 0)
        {
            string key = locatingGameMoney ? "ui.locating" :
                (moneyLocateError == null ? "ui.waiting_for_locator" : "ui.location_failed");
            SetMoneyValueMessage(key);
            return;
        }

        int value, error;
        if (RadiantDefenseMoneyLocator.TryReadMoney(gameHandle, gameMoneyAddress, out value, out error))
        {
            SetMoneyValue(value);
            return;
        }

        HandleBalanceReadFailure(error);
    }

    internal void HandleBalanceReadFailure(int error)
    {
        gameMoneyAddress = 0;
        moneyLocateError = new LocalizedMessage("status.balance_read_failed", error);
        SetMoneyValueMessage("ui.read_failed");
        SetMoneyStatus("ui.relocating");
        nextMoneyLocateTick = tickCount + 2;
    }

    void BeginMoneyLocate()
    {
        if (!autoConnected || locatingGameMoney || gamePid < 0) return;
        locatingGameMoney = true;
        int pid = gamePid;
        SetMoneyStatus("ui.locating");

        bool queued = ThreadPool.QueueUserWorkItem(delegate
        {
            IntPtr handle = MemApi.OpenTarget(pid);
            long gameObjectAddress = 0;
            long moneyAddress = 0;
            int balance = 0;
            LocalizedMessage error = null;
            try
            {
                if (handle == IntPtr.Zero)
                    error = new LocalizedMessage("status.open_process_failed", Marshal.GetLastWin32Error());
                else
                    RadiantDefenseMoneyLocator.TryLocate(handle, pid,
                        out gameObjectAddress, out moneyAddress, out balance, out error);
            }
            catch (Exception ex)
            {
                error = new LocalizedMessage("locator.unexpected", ex.Message);
            }
            finally
            {
                if (handle != IntPtr.Zero) MemApi.CloseHandle(handle);
            }

            try
            {
                BeginInvoke((Action)(() => MoneyLocateCompleted(pid, moneyAddress, balance, error)));
            }
            catch (InvalidOperationException) { }
        });

        if (!queued)
        {
            locatingGameMoney = false;
            SetMoneyStatus("status.locator_task_failed");
            SetStatus("status.locator_task_failed");
        }
    }

    void MoneyLocateCompleted(int pid, long moneyAddress, int balance, LocalizedMessage error)
    {
        if (pid != gamePid || !autoConnected) return;
        locatingGameMoney = false;
        if (moneyAddress == 0)
        {
            moneyLocateError = error ?? new LocalizedMessage("locator.instance_missing");
            SetMoneyStatus("ui.location_retrying");
            SetMoneyValueMessage("ui.locating");
            nextMoneyLocateTick = tickCount + 10;
            if (!string.Equals(lastLoggedLocateErrorSignature, moneyLocateError.Signature, StringComparison.Ordinal))
            {
                SetStatus("status.locator_failed", moneyLocateError);
                lastLoggedLocateErrorSignature = moneyLocateError.Signature;
            }
            return;
        }

        gameMoneyAddress = moneyAddress;
        moneyLocateError = null;
        lastLoggedLocateErrorSignature = null;
        nextMoneyLocateTick = 0;
        SetMoneyStatus("ui.location_succeeded");
        SetStatus("status.locator_succeeded", balance);
        ResolveAndShow();
    }

    void AutoSet()
    {
        double parsed;
        int value;
        if (!TryParseSet(out parsed))
        {
            SetStatus("status.invalid_set_value");
            return;
        }
        if (!TryConvertMoney(parsed, out value))
        {
            SetStatus("status.set_value_range");
            return;
        }

        DetectGame();
        ResolveAndShow();
        if (gameMoneyAddress == 0)
        {
            if (moneyLocateError == null) SetStatus("status.locating");
            else SetStatus("status.locator_failed", moneyLocateError);
            return;
        }

        int error, restoreError;
        bool mismatch;
        if (RadiantDefenseMoneyLocator.TryWriteMoney(gameHandle, gameMoneyAddress, value,
            out error, out restoreError, out mismatch))
        {
            if (restoreError != 0)
                SetStatus("status.set_success_with_warning", value,
                    MoneyWriteProblem(error, restoreError, mismatch));
            else
                SetStatus("status.set_success", value);
        }
        else
            SetStatus("status.set_failure", MoneyWriteProblem(error, restoreError, mismatch));
        ResolveAndShow();
    }

    void AutoAdd()
    {
        double parsedAmount;
        int amount;
        if (!TryParseAdd(out parsedAmount)) return;
        if (!TryConvertMoney(parsedAmount, out amount))
        {
            SetStatus("status.add_value_range");
            return;
        }

        DetectGame();
        ResolveAndShow();
        if (gameMoneyAddress == 0)
        {
            if (moneyLocateError == null) SetStatus("status.locating");
            else SetStatus("status.locator_failed", moneyLocateError);
            return;
        }

        int current, readError;
        if (!RadiantDefenseMoneyLocator.TryReadMoney(gameHandle, gameMoneyAddress, out current, out readError))
        {
            SetStatus("status.read_current_failed", readError);
            return;
        }

        long next = (long)current + amount;
        if (next > int.MaxValue)
        {
            SetStatus("status.add_overflow");
            return;
        }

        int error, restoreError;
        bool mismatch;
        if (RadiantDefenseMoneyLocator.TryWriteMoney(gameHandle, gameMoneyAddress, (int)next,
            out error, out restoreError, out mismatch))
        {
            if (chkLock.Checked) txtSet.Text = next.ToString(CultureInfo.InvariantCulture);
            if (restoreError != 0)
                SetStatus("status.add_success_with_warning", current, amount, next,
                    MoneyWriteProblem(error, restoreError, mismatch));
            else
                SetStatus("status.add_success", current, amount, next);
        }
        else
            SetStatus("status.add_failure", MoneyWriteProblem(error, restoreError, mismatch));
        ResolveAndShow();
    }

    static bool TryConvertMoney(double value, out int money)
    {
        money = 0;
        if (double.IsNaN(value) || double.IsInfinity(value) ||
            value < int.MinValue || value > int.MaxValue || Math.Truncate(value) != value)
            return false;
        money = (int)value;
        return true;
    }

    LocalizedMessage MoneyWriteProblem(int error, int restoreError, bool mismatch)
    {
        if (mismatch) return new LocalizedMessage("error.write_mismatch");
        if (restoreError != 0) return new LocalizedMessage("error.page_protection_restore", restoreError);
        if (error == 5) return new LocalizedMessage("error.access_denied");
        if (error != 0) return new LocalizedMessage("error.write_failed", error);
        return new LocalizedMessage("error.write_verify_failed");
    }

    void AutoFreezeTick()
    {
        if (!chkLock.Checked)
        {
            lockWriteWarningShown = false;
            return;
        }
        if (gameMoneyAddress == 0 || gameHandle == IntPtr.Zero) return;

        double parsed;
        int value;
        if (!TryParseSet(out parsed) || !TryConvertMoney(parsed, out value)) return;

        int error, restoreError;
        bool mismatch;
        bool ok = RadiantDefenseMoneyLocator.TryWriteMoney(gameHandle, gameMoneyAddress, value,
            out error, out restoreError, out mismatch);
        if ((!ok || restoreError != 0) && !lockWriteWarningShown)
        {
            SetStatus("status.lock_failure", MoneyWriteProblem(error, restoreError, mismatch));
            lockWriteWarningShown = true;
        }
        else if (ok && restoreError == 0)
            lockWriteWarningShown = false;
    }

    void StartRelocate()
    {
        DetectGame();
        if (!autoConnected)
        {
            SetStatus("status.game_required");
            return;
        }

        gameMoneyAddress = 0;
        moneyLocateError = null;
        lastLoggedLocateErrorSignature = null;
        nextMoneyLocateTick = 0;
        SetMoneyValueMessage("ui.locating");
        SetMoneyStatus("ui.relocating");
        SetStatus("status.relocating");
        BeginMoneyLocate();
    }

    bool TryParseSet(out double value)
    {
        value = 0;
        string text = txtSet.Text.Trim();
        return text.Length > 0 &&
            (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
             double.TryParse(text, out value));
    }

    bool TryParseAdd(out double amount)
    {
        amount = 0;
        string text = txtAdd.Text.Trim();
        if (text.Length == 0 ||
            (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out amount) &&
             !double.TryParse(text, out amount)) ||
            amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount))
        {
            SetStatus("status.add_value_invalid");
            return false;
        }
        return true;
    }

    void AutoTick(object sender, EventArgs e)
    {
        tickCount++;
        if (tickCount % 2 == 0) DetectGame();
        if (autoConnected)
        {
            if (gameMoneyAddress == 0 && !locatingGameMoney && tickCount >= nextMoneyLocateTick)
                BeginMoneyLocate();
            ResolveAndShow();
        }
        AutoFreezeTick();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        autoTimer.Stop();
        if (gameHandle != IntPtr.Zero)
        {
            MemApi.CloseHandle(gameHandle);
            gameHandle = IntPtr.Zero;
        }
        base.OnFormClosed(e);
    }
}
