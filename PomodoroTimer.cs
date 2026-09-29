using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;

class Ring : Control
{
    public float Progress;
    public string TimeText = "", PhaseText = "";
    public Color Accent = Color.Tomato;

    public Ring()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        const int w = 14;
        var r = new Rectangle(w, w, Width - 2 * w - 1, Height - 2 * w - 1);
        using (var bg = new Pen(Color.FromArgb(235, 235, 235), w))
            g.DrawEllipse(bg, r);
        if (Progress > 0)
            using (var fg = new Pen(Accent, w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(fg, r, -90, 360 * Math.Min(Progress, 1f));

        var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using (var big = new Font("Segoe UI Light", 44))
        using (var small = new Font("Segoe UI", 12))
        using (var accent = new SolidBrush(Accent))
        {
            g.DrawString(TimeText, big, Brushes.Black, new RectangleF(0, -10, Width, Height), center);
            g.DrawString(PhaseText, small, accent, new RectangleF(0, 70, Width, Height), center);
        }
    }
}

class PomodoroTimer : Form
{
    const int Work = 0, ShortBreak = 1, LongBreak = 2;
    static readonly string[] PhaseNames = { "Работа", "Короткий перерыв", "Длинный перерыв" };
    static readonly Color[] PhaseColors = { Color.FromArgb(231, 76, 60), Color.FromArgb(39, 174, 96), Color.FromArgb(41, 128, 185) };

    Ring ring;
    Button startButton;
    NumericUpDown workBox, shortBox, longBox;
    Label counterLabel;
    NotifyIcon tray;
    Timer timer;

    int phase = Work, completed;
    bool running, fresh = true;
    DateTime endTime;
    TimeSpan remaining;

    PomodoroTimer()
    {
        Text = "Pomodoro";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(340, 480);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;

        ring = new Ring { Location = new Point(20, 10), Size = new Size(300, 300) };
        Controls.Add(ring);

        startButton = new Button { Text = "Старт", Location = new Point(20, 322), Size = new Size(140, 42) };
        startButton.Click += (s, e) => StartPause();
        var resetButton = new Button { Text = "Сброс", Location = new Point(170, 322), Size = new Size(70, 42) };
        resetButton.Click += (s, e) => ResetPhase();
        var skipButton = new Button { Text = "Далее", Location = new Point(250, 322), Size = new Size(70, 42) };
        skipButton.Click += (s, e) => NextPhase(false);
        Controls.Add(startButton);
        Controls.Add(resetButton);
        Controls.Add(skipButton);

        workBox = AddSetting("Работа, мин", 20, 25);
        shortBox = AddSetting("Перерыв", 125, 5);
        longBox = AddSetting("Длинный", 230, 15);

        counterLabel = new Label { Location = new Point(20, 448), AutoSize = true, ForeColor = Color.DimGray };
        Controls.Add(counterLabel);

        tray = new NotifyIcon { Icon = SystemIcons.Information, Text = "Pomodoro", Visible = true };
        tray.DoubleClick += (s, e) => { Show(); WindowState = FormWindowState.Normal; Activate(); };

        timer = new Timer { Interval = 200 };
        timer.Tick += (s, e) => Tick();

        ResetPhase();
    }

    NumericUpDown AddSetting(string label, int x, int value)
    {
        Controls.Add(new Label { Text = label, Location = new Point(x, 378), AutoSize = true, ForeColor = Color.DimGray });
        var box = new NumericUpDown { Location = new Point(x, 400), Width = 90, Minimum = 1, Maximum = 180, Value = value };
        box.ValueChanged += (s, e) => { if (fresh) ResetPhase(); };
        Controls.Add(box);
        return box;
    }

    TimeSpan PhaseLength()
    {
        var box = phase == Work ? workBox : phase == ShortBreak ? shortBox : longBox;
        return TimeSpan.FromMinutes((double)box.Value);
    }

    void StartPause()
    {
        if (running)
        {
            remaining = endTime - DateTime.Now;
            running = false;
            timer.Stop();
        }
        else
        {
            endTime = DateTime.Now + remaining;
            running = true;
            fresh = false;
            timer.Start();
        }
        UpdateUi();
    }

    void ResetPhase()
    {
        running = false;
        fresh = true;
        timer.Stop();
        remaining = PhaseLength();
        UpdateUi();
    }

    void Tick()
    {
        var left = endTime - DateTime.Now;
        if (left <= TimeSpan.Zero) { NextPhase(true); return; }
        remaining = left;
        UpdateUi();
    }

    void NextPhase(bool finished)
    {
        string message;
        if (phase == Work)
        {
            if (finished) completed++;
            phase = finished && completed % 4 == 0 ? LongBreak : ShortBreak;
            message = "Помидор готов! Время отдохнуть.";
        }
        else
        {
            phase = Work;
            message = "Перерыв окончен. За работу!";
        }
        ResetPhase();
        if (finished)
        {
            SystemSounds.Exclamation.Play();
            tray.ShowBalloonTip(5000, "Pomodoro", message, ToolTipIcon.Info);
        }
    }

    void UpdateUi()
    {
        var shown = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        // round up so the display shows 25:00 at the start and 00:01 in the last second
        int secs = (int)Math.Ceiling(shown.TotalSeconds);
        string time = string.Format("{0:00}:{1:00}", secs / 60, secs % 60);

        ring.TimeText = time;
        ring.PhaseText = PhaseNames[phase] + (running ? "" : fresh ? "" : " · пауза");
        ring.Accent = PhaseColors[phase];
        ring.Progress = (float)(1 - shown.TotalSeconds / PhaseLength().TotalSeconds);
        ring.Invalidate();

        startButton.Text = running ? "Пауза" : fresh ? "Старт" : "Продолжить";
        counterLabel.Text = "Помидоров за сессию: " + completed + "   " + new string('●', completed % 4) + new string('○', 4 - completed % 4);
        Text = running ? time + " — Pomodoro" : "Pomodoro";
        tray.Text = PhaseNames[phase] + ": " + time;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosing(e);
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new PomodoroTimer());
    }
}
