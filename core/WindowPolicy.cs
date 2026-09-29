using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// One owner for both windows' visibility and native topmost state.
sealed class WindowPolicy : IDisposable {
    readonly Form pet;
    Form info;
    readonly string settings;
    readonly Timer timer = new Timer();
    readonly ToolStripMenuItem pinItem = new ToolStripMenuItem("窗口置顶");
    readonly ToolStripMenuItem fullscreenItem = new ToolStripMenuItem("全屏时自动隐藏");
    bool pinned = true, avoidFullscreen = true, suppressed, infoWanted;
    public WindowPolicy(Form owner, ContextMenuStrip menu, string path, bool testing) {
        pet = owner; settings = path;
        if (File.Exists(path)) {
            string[] lines = File.ReadAllLines(path); bool value;
            if (lines.Length > 0 && bool.TryParse(lines[0], out value)) pinned = value;
            if (lines.Length > 1 && bool.TryParse(lines[1], out value)) avoidFullscreen = value;
        }
        pinItem.Checked = pinned; fullscreenItem.Checked = avoidFullscreen;
        pinItem.Click += delegate { SetPinned(!pinned); Save(); Refresh(); };
        fullscreenItem.Click += delegate { avoidFullscreen = !avoidFullscreen; fullscreenItem.Checked = avoidFullscreen; Save(); Refresh(); };
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add(pinItem); menu.Items.Add(fullscreenItem);
        pet.TopMost = pinned;
        timer.Interval = 500; timer.Tick += delegate { Refresh(); };
        if (!testing) timer.Start();
    }
    void Save() { Directory.CreateDirectory(Path.GetDirectoryName(settings)); File.WriteAllLines(settings, new[] { pinned.ToString(), avoidFullscreen.ToString() }); }
    public void AttachInfo(Form window) {
        info = window; info.TopMost = pinned;
        if (info.ContextMenuStrip != null) {
            ToolStripMenuItem pin = new ToolStripMenuItem("窗口置顶"), full = new ToolStripMenuItem("全屏时自动隐藏");
            pin.Click += delegate { pinItem.PerformClick(); }; full.Click += delegate { fullscreenItem.PerformClick(); };
            info.ContextMenuStrip.Items.Add(new ToolStripSeparator()); info.ContextMenuStrip.Items.Add(pin); info.ContextMenuStrip.Items.Add(full);
            info.ContextMenuStrip.Opening += delegate { pin.Checked = pinned; full.Checked = avoidFullscreen; };
        }
        Sync();
    }
    public void ToggleInfo() { infoWanted = !infoWanted; if (!infoWanted) info.Hide(); else if (!suppressed) info.Show(pet); Sync(); }
    public void ShowPet() { Refresh(); if (!suppressed) pet.Show(); Sync(); }
    public void SetPinned(bool value) { pinned = value; pinItem.Checked = value; ApplyFullscreen(false); Sync(); }
    public void Refresh() {
        IntPtr foreground = GetForegroundWindow();
        // A null foreground window during switching should not briefly reveal the pet.
        bool full = foreground == IntPtr.Zero ? suppressed : IsFullscreen(foreground);
        ApplyFullscreen(pinned && avoidFullscreen && full); Sync();
    }
    public void ApplyFullscreen(bool hide) {
        if (suppressed == hide) return;
        suppressed = hide;
        if (hide) { if (info != null) info.Hide(); pet.Hide(); }
        else { pet.Show(); if (infoWanted && info != null) info.Show(pet); }
    }
    void Sync() { SetWindowPinned(pet, pinned); if (info != null && !info.IsDisposed) SetWindowPinned(info, pinned); }
    static void SetWindowPinned(Form form, bool value) {
        if (form.TopMost != value) form.TopMost = value;
        if (form.IsHandleCreated && NativePinned(form) != value)
            SetWindowPos(form.Handle, new IntPtr(value ? -1 : -2), 0, 0, 0, 0, 0x213);
    }
    static bool NativePinned(Form form) { return (GetWindowLong(form.Handle, -20) & 8) != 0; }
    static bool CoversScreen(Rectangle window, Rectangle screen, int style) {
        // A maximized captioned app is not a fullscreen game, even with an auto-hidden taskbar.
        return (style & 0x00C00000) == 0 && window.Left <= screen.Left + 2 && window.Top <= screen.Top + 2 && window.Right >= screen.Right - 2 && window.Bottom >= screen.Bottom - 2;
    }
    static bool IsFullscreen(IntPtr window) {
        uint process; GetWindowThreadProcessId(window, out process);
        if (process == GetCurrentProcessId() || !IsWindowVisible(window) || IsIconic(window)) return false;
        StringBuilder name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
        string cls = name.ToString();
        if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
        RECT rect; if (!GetWindowRect(window, out rect)) return false;
        return CoversScreen(Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), Screen.FromHandle(window).Bounds, GetWindowLong(window, -16));
    }
    public static void Test(string folder) {
        string path = Path.Combine(folder, "test-window-policy.txt");
        if (File.Exists(path)) File.WriteAllLines(path, new[] { "True", "True" });
        using (Form owner = new Form()) using (Form child = new Form()) using (ContextMenuStrip menu = new ContextMenuStrip())
        using (WindowPolicy policy = new WindowPolicy(owner, menu, path, true)) {
            owner.Show(); policy.AttachInfo(child); policy.ToggleInfo();
            policy.SetPinned(false);
            if (NativePinned(owner) || NativePinned(child)) throw new Exception("Unpin was not synchronized");
            policy.SetPinned(true);
            if (!NativePinned(owner) || !NativePinned(child)) throw new Exception("Pin was not synchronized");
            SetWindowPos(owner.Handle, new IntPtr(-2), 0, 0, 0, 0, 0x213); policy.Sync();
            if (!NativePinned(owner) || !NativePinned(child)) throw new Exception("Topmost drift was not repaired");
            policy.ApplyFullscreen(true);
            if (owner.Visible || child.Visible) throw new Exception("Fullscreen did not hide both windows");
            policy.ApplyFullscreen(false);
            if (!owner.Visible || !child.Visible) throw new Exception("Fullscreen restore failed");
            policy.ToggleInfo(); policy.ApplyFullscreen(true); policy.ApplyFullscreen(false);
            if (child.Visible) throw new Exception("Closed information window reopened");
            policy.SetPinned(false); policy.Save();
        }
        using (Form owner = new Form()) using (ContextMenuStrip menu = new ContextMenuStrip())
        using (WindowPolicy loaded = new WindowPolicy(owner, menu, path, true)) {
            if (loaded.pinned) throw new Exception("Topmost setting was not saved");
        }
        Rectangle monitor = new Rectangle(-1920, 0, 1920, 1080);
        if (!CoversScreen(monitor, monitor, 0) || CoversScreen(monitor, monitor, 0x00C00000) || CoversScreen(new Rectangle(-1800, 0, 1000, 800), monitor, 0)) throw new Exception("Fullscreen geometry detection failed");
    }
    public void Dispose() { timer.Stop(); timer.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
}
