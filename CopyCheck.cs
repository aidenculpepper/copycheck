using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Reflection;
[assembly: AssemblyTitle("CopyCheck")]
[assembly: AssemblyProduct("CopyCheck")]
[assembly: AssemblyVersion("1.0.9.0")]
[assembly: AssemblyFileVersion("1.0.9.0")]

namespace CopyCheck {
static class Program {
    [STAThread] static int Main(string[] args) {
        if(Array.IndexOf(args,"--install-latest") >= 0) return Updates.Bootstrap();
        Native.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if(Array.IndexOf(args,"--configure-install") >= 0 || Array.IndexOf(args,"--remove-install") >= 0) {
            string step = "checking administrator permission";
            try {
                if(!Elevation.IsAdministrator) throw new InvalidOperationException("Setup requires administrator permission.");
                if(Array.IndexOf(args,"--configure-install") >= 0) {
                    step = "checking the signed-in account";
                    ScheduledStartup.CheckInstallerAccount();
                    step = "checking the existing startup task";
                    bool upgrading = ScheduledStartup.HasTask;
                    bool administrator = upgrading ? AdminPreference.Enabled : true;
                    bool startup = upgrading ? Startup.IsEnabled : true;
                    step = "registering the startup task";
                    ScheduledStartup.Configure(administrator,startup);
                    step = "saving startup preferences";
                    AdminPreference.Enabled = administrator;
                    Startup.RemoveLegacyShortcut();
                } else ScheduledStartup.RemoveAllInstalledTasks();
                return 0;
            } catch(Exception ex) {
                MessageBox.Show("Failed while "+step+".\n"+ex.Message+"\nError: 0x"+ex.HResult.ToString("X8"),"CopyCheck setup",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1;
            }
        }
        for(int i = 0; i < args.Length-1; i++) {
            int pid;
            if(args[i] == "--wait-for" && int.TryParse(args[i+1],out pid)) {
                try { using(var previous = Process.GetProcessById(pid)) { if(!previous.WaitForExit(15000)) return 1; } }
                catch(ArgumentException) { }
            }
        }
        if(AdminPreference.Enabled && !Elevation.IsAdministrator) {
            try {
                if(ScheduledStartup.TryLaunch()) return 0;
                Elevation.Launch(0,Array.IndexOf(args,"--show-settings") >= 0,Array.IndexOf(args,"--animation-off") >= 0); return 0;
            }
            catch(System.ComponentModel.Win32Exception ex) {
                if(ex.NativeErrorCode != 1223) MessageBox.Show("Could not start as administrator.\n"+ex.Message,"CopyCheck",MessageBoxButtons.OK,MessageBoxIcon.Error);
                // A declined UAC prompt leaves a usable standard-permission instance.
            }
        }
        bool created;
        using (var mutex = new System.Threading.Mutex(true, "Local\\CopyCheck.Foundation", out created)) {
            if (!created) return 0;
            Application.Run(new CopyContext(Array.IndexOf(args,"--show-settings") >= 0,Array.IndexOf(args,"--animation-off") < 0 && AppPreferences.Animation));
        }
        return 0;
    }
}
class CopyContext : ApplicationContext {
    readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer();
    bool exiting;
    readonly NotifyIcon tray;
    readonly Icon trayIcon;
    readonly Listener listener;
    readonly Native.HookProc callback;
    readonly System.Windows.Forms.Timer expiry = new System.Windows.Forms.Timer();
    IntPtr hook;
    uint sequence;
    IntPtr source;
    DateTime pendingUntil;
    bool enabled = true, pending, cDown;
    Form settings;
    public CopyContext(bool showSettings, bool animationEnabled) {
        enabled = animationEnabled;
        if(ScheduledStartup.Installed && Elevation.IsAdministrator && ScheduledStartup.AtLogon) ScheduledStartup.Configure(AdminPreference.Enabled,Startup.IsEnabled);
        if(ScheduledStartup.Installed && Elevation.IsAdministrator && AdminPreference.Enabled) {
            // Complete a user-approved change from standard to elevated mode.
            if(!ScheduledStartup.Highest) ScheduledStartup.Configure(true,Startup.IsEnabled);
        }
        listener = new Listener(this);
        callback = OnKey;
        hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        trayIcon = Brand.CreateIcon();
        tray = new NotifyIcon { Icon = trayIcon, Text = "CopyCheck", Visible = true };
        tray.MouseClick += delegate(object s, MouseEventArgs e) { if(e.Button == MouseButtons.Left) ShowSettings(); };
        var menu = new ContextMenuStrip { BackColor = Brand.Surface, ForeColor = Brand.Text, ShowImageMargin = false, Font = new Font("Segoe UI", 10), Renderer = new DarkMenuRenderer() };
        menu.Items.Add("Settings", null, delegate { ShowSettings(); });
        menu.Items.Add("Exit", null, delegate { ExitThread(); });
        tray.ContextMenuStrip = menu;
        expiry.Interval = 100;
        expiry.Tick += delegate { if (pending && DateTime.UtcNow > pendingUntil) pending = false; };
        expiry.Start();
        tray.BalloonTipClicked += delegate { ShowSettings(); };
        updateTimer.Interval = 21600000;
        updateTimer.Tick += async delegate { if(AppPreferences.AutomaticUpdates) await CheckForUpdates(); };
        updateTimer.Start();
        listener.BeginInvoke((Action)(async delegate { if(AppPreferences.AutomaticUpdates) await CheckForUpdates(); }));
        if(showSettings) listener.BeginInvoke((Action)ShowSettings);
    }
    async System.Threading.Tasks.Task CheckForUpdates() {
        await Updates.CheckAsync();
        if(!exiting && Updates.Available != null) { tray.BalloonTipTitle = "CopyCheck update available"; tray.BalloonTipText = "Version "+Updates.Available.Version+" is ready. Open Settings to install."; tray.ShowBalloonTip(6000); }
    }
    IntPtr OnKey(int code, IntPtr w, IntPtr l) {
        if(code >= 0) {
            var key = (Native.Keyboard)Marshal.PtrToStructure(l, typeof(Native.Keyboard));
            int msg = w.ToInt32();
            if (key.vk == 0x43) {
                if(msg == 0x101 || msg == 0x105) cDown = false;
                if((msg == 0x100 || msg == 0x104) && !cDown) {
                    cDown = true;
                    if(enabled && (key.flags & 0x10) == 0 && (Native.GetAsyncKeyState(0x11) & 0x8000) != 0
                       && (Native.GetAsyncKeyState(0x12) & 0x8000) == 0) {
                        sequence = Native.GetClipboardSequenceNumber();
                        source = Native.GetForegroundWindow();
                        pendingUntil = DateTime.UtcNow.AddMilliseconds(1500);
                        pending = true;
                    }
                }
            }
        }
        return Native.CallNextHookEx(hook, code, w, l);
    }
    Point? SelectionAnchor() {
        try {
            var element = AutomationElement.FocusedElement;
            object pattern;
            if(element != null && element.TryGetCurrentPattern(TextPattern.Pattern, out pattern)) {
                var ranges = ((TextPattern)pattern).GetSelection();
                if(ranges.Length > 0) {
                    var last = ranges[ranges.Length - 1];
                    var tail = last.Clone();
                    tail.MoveEndpointByRange(TextPatternRangeEndpoint.Start, last, TextPatternRangeEndpoint.End);
                    tail.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -1);
                    var rect = tail.GetBoundingRectangles();
                    if(rect.Length == 0 || rect[rect.Length-1].Width <= 0) rect = last.GetBoundingRectangles();
                    for(int i = rect.Length-1; i >= 0; i--) {
                        if(rect[i].Width > 0 && rect[i].Height > 0)
                            return new Point((int)Math.Round(rect[i].Right), (int)Math.Round(rect[i].Y + rect[i].Height/2));
                    }
                }
            }
        } catch { }
        return null;
    }
    internal void ClipboardChanged() {
        if(!enabled || !pending || DateTime.UtcNow > pendingUntil || Native.GetClipboardSequenceNumber() == sequence) return;
        // Require a populated clipboard owned by the application that received Ctrl+C.
        uint sourcePid, ownerPid;
        Native.GetWindowThreadProcessId(source, out sourcePid);
        Native.GetWindowThreadProcessId(Native.GetClipboardOwner(), out ownerPid);
        if(sourcePid == 0 || sourcePid != ownerPid || Native.CountClipboardFormats() == 0) return;
        pending = false;
        var anchor = SelectionAnchor();
        if(anchor.HasValue) new CheckOverlay(anchor.Value).Show();
    }
    void ShowSettings() {
        if(settings != null && !settings.IsDisposed) { settings.Activate(); return; }
        settings = new SettingsForm(enabled);
        ((SettingsForm)settings).AnimationChanged += delegate(bool value) { enabled = value; AppPreferences.Animation = value; pending = false; };
        ((SettingsForm)settings).AdministratorRestarted += delegate { ExitThread(); };
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        settings.Location = new Point(area.Left+(area.Width-settings.Width)/2, area.Top+(area.Height-settings.Height)/2);
        settings.Show();
    }
    protected override void ExitThreadCore() {
        exiting=true; updateTimer.Dispose();
        Native.UnhookWindowsHookEx(hook);
        expiry.Dispose(); tray.Visible = false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); trayIcon.Dispose(); listener.Dispose();
        if(settings != null) settings.Dispose();
        base.ExitThreadCore();
    }
}
static class Brand {
    public static readonly Color Background = Color.FromArgb(18,21,27), Surface = Color.FromArgb(28,33,41), Text = Color.FromArgb(234,239,245), Accent = Color.FromArgb(63,222,160);
    public static Icon CreateIcon() {
        using(var bitmap = CreateBitmap(32)) {
            IntPtr handle = bitmap.GetHicon();
            try { using(var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
            finally { Native.DestroyIcon(handle); }
        }
    }
    public static Bitmap CreateBitmap(int size) {
        var bitmap = new Bitmap(size,size);
        try {
            using(var g = Graphics.FromImage(bitmap)) {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.ScaleTransform(size/32f,size/32f);
                using(var fill = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(2,2,28,28),Color.FromArgb(66,231,167),Color.FromArgb(15,156,118),45f)) g.FillEllipse(fill,2,2,28,28);
                using(var pen = new Pen(Color.FromArgb(15,35,31),3.2f)) {
                    pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                    g.DrawLines(pen,new PointF[] { new PointF(9,16),new PointF(14,21),new PointF(23,11) });
                }
            }
            return bitmap;
        } catch { bitmap.Dispose(); throw; }
    }
}
class DarkMenuRenderer : ToolStripProfessionalRenderer {
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
        using(var brush = new SolidBrush(e.Item.Selected ? Color.FromArgb(43,52,63) : Brand.Surface)) e.Graphics.FillRectangle(brush,new Rectangle(Point.Empty,e.Item.Size));
    }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
}
class Toggle : CheckBox {
    public Toggle() {
        AutoSize = false; Size = new Size(330,52); ForeColor = Brand.Text; BackColor = Brand.Surface;
        Font = new Font("Segoe UI",10); Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,true);
        AccessibleRole = AccessibleRole.CheckButton;
    }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        TextRenderer.DrawText(g,Text,Font,new Rectangle(16,0,Width-84,Height),ForeColor,TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        int x = Width-60, y = (Height-22)/2;
        using(var brush = new SolidBrush(Checked ? Brand.Accent : Color.FromArgb(72,81,94))) {
            g.FillEllipse(brush,x,y,22,22); g.FillRectangle(brush,x+11,y,18,22); g.FillEllipse(brush,x+18,y,22,22);
        }
        using(var brush = new SolidBrush(Checked ? Color.FromArgb(16,43,34) : Color.FromArgb(218,225,235))) g.FillEllipse(brush,x+(Checked ? 21 : 3),y+3,16,16);
        if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-10,Height-10),ForeColor,BackColor);
    }
}
class SettingsForm : Form {
    public event Action<bool> AnimationChanged;
    public event Action AdministratorRestarted;
    readonly Icon icon;
    public SettingsForm(bool enabled) {
        Text = "CopyCheck"; ClientSize = new Size(378,597); BackColor = Brand.Background; ForeColor = Brand.Text;
        Font = new Font("Segoe UI",10); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.Dpi;
        icon = Brand.CreateIcon(); Icon = icon;
        Controls.Add(new Label { Text = "Settings", AutoSize = true, Font = new Font("Segoe UI Semibold",19), Location = new Point(24,22), ForeColor = Brand.Text });
        var animation = new Toggle { Text = "Copy checkmark", Checked = enabled, Location = new Point(24,83) };
        animation.CheckedChanged += delegate { if(AnimationChanged != null) AnimationChanged(animation.Checked); };
        Controls.Add(animation);
        var startup = new Toggle { Text = "Start on startup", Checked = Startup.IsEnabled, Location = new Point(24,147) };
        bool changing = false;
        startup.CheckedChanged += delegate {
            if(changing) return;
            try { Startup.SetEnabled(startup.Checked); }
            catch(Exception ex) {
                changing = true; startup.Checked = Startup.IsEnabled; changing = false;
                MessageBox.Show(this,"Could not change startup setting.\n"+ex.Message,"CopyCheck",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        };
        Controls.Add(startup);
        var admin = new Toggle { Text = "Run as administrator", Checked = AdminPreference.Enabled, Location = new Point(24,211) };
        bool adminChanging = false;
        admin.CheckedChanged += delegate {
            if(adminChanging) return;
            bool previous = AdminPreference.Enabled;
            try {
                AdminPreference.Enabled = admin.Checked;
                if(admin.Checked && !Elevation.IsAdministrator) {
                    try { Elevation.Launch(Process.GetCurrentProcess().Id,true,!animation.Checked); }
                    catch { AdminPreference.Enabled = previous; throw; }
                    if(AdministratorRestarted != null) AdministratorRestarted();
                } else if(ScheduledStartup.Installed && Elevation.IsAdministrator) {
                    try { ScheduledStartup.Configure(admin.Checked,Startup.IsEnabled); }
                    catch { AdminPreference.Enabled = previous; throw; }
                }
            } catch(Exception ex) {
                adminChanging = true; admin.Checked = AdminPreference.Enabled; adminChanging = false;
                var win32 = ex as System.ComponentModel.Win32Exception;
                if(win32 == null || win32.NativeErrorCode != 1223) MessageBox.Show(this,"Could not change administrator setting.\n"+ex.Message,"CopyCheck",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        };
        Controls.Add(admin);
        AddUpdateControls();
    }

    Label updateStatus;
    Button checkUpdates, installUpdate;
    void AddUpdateControls() {
        Controls.Add(new Label { Text="Updates",AutoSize=true,Font=new Font("Segoe UI Semibold",12),Location=new Point(24,284),ForeColor=Brand.Text });
        Controls.Add(new Label { Text="Installed version "+Updates.Current,AutoSize=true,Location=new Point(24,313),ForeColor=Color.FromArgb(155,168,183),Font=new Font("Segoe UI",9) });
        var automatic=new Toggle { Text="Check automatically",Checked=AppPreferences.AutomaticUpdates,Location=new Point(24,343) };
        automatic.CheckedChanged += delegate {
            try { AppPreferences.AutomaticUpdates=automatic.Checked; }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"CopyCheck",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        };
        Controls.Add(automatic);
        updateStatus=new Label { Location=new Point(24,405),Size=new Size(330,48),ForeColor=Color.FromArgb(155,168,183),Font=new Font("Segoe UI",9) };
        Controls.Add(updateStatus);
        checkUpdates=UpdateButton("Check for updates",new Point(24,465),158);
        installUpdate=UpdateButton("Install update",new Point(196,465),158);
        checkUpdates.Click += async delegate { await Updates.CheckAsync(); };
        installUpdate.Click += async delegate {
            if(Updates.Busy || Updates.Available == null) return;
            var release=Updates.Available;
            Updates.Busy=true; RefreshUpdates();
            updateStatus.Text="Downloading and verifying the installer...";
            try {
                string file=await System.Threading.Tasks.Task.Run(()=>Updates.Download(release));
                Updates.LaunchInstaller(file);
                if(AdministratorRestarted != null) AdministratorRestarted();
            } catch(Exception ex) {
                var native=ex as System.ComponentModel.Win32Exception;
                Updates.Status=native != null && native.NativeErrorCode == 1223 ? "Installation cancelled. You can try again." : ex.Message;
            } finally { Updates.Busy=false; if(!IsDisposed) RefreshUpdates(); }
        };
        Updates.Changed += RefreshUpdates;
        FormClosed += delegate { Updates.Changed -= RefreshUpdates; };
        var uninstall=UpdateButton("Uninstall CopyCheck",new Point(24,531),330);
        uninstall.Enabled=ScheduledStartup.Installed && File.Exists(Path.Combine(Application.StartupPath,"unins000.exe"));
        uninstall.Click += delegate {
            try {
                using(var process=Process.Start(new ProcessStartInfo { FileName=Path.Combine(Application.StartupPath,"unins000.exe"),WorkingDirectory=Application.StartupPath,UseShellExecute=true,Verb="runas" })) {
                    if(process == null) throw new IOException("Windows did not start the uninstaller.");
                }
                if(AdministratorRestarted != null) AdministratorRestarted();
            } catch(System.ComponentModel.Win32Exception ex) { if(ex.NativeErrorCode != 1223) MessageBox.Show(this,ex.Message,"CopyCheck",MessageBoxButtons.OK,MessageBoxIcon.Error); }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"CopyCheck",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        };
        RefreshUpdates();
    }
    Button UpdateButton(string text,Point location,int width) {
        var button=new Button { Text=text,Location=location,Size=new Size(width,38),FlatStyle=FlatStyle.Flat,BackColor=Brand.Surface,ForeColor=Brand.Text,Cursor=Cursors.Hand };
        button.FlatAppearance.BorderColor=Color.FromArgb(65,76,89);
        Controls.Add(button); return button;
    }
    void RefreshUpdates() {
        if(IsDisposed) return;
        if(InvokeRequired) { if(IsHandleCreated) BeginInvoke((Action)RefreshUpdates); return; }
        updateStatus.Text=Updates.Status;
        checkUpdates.Enabled=!Updates.Busy;
        installUpdate.Enabled=!Updates.Busy && Updates.Available != null;
        installUpdate.Visible=Updates.Available != null;
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        try { int dark = 1; if(Native.DwmSetWindowAttribute(Handle,20,ref dark,4) != 0) Native.DwmSetWindowAttribute(Handle,19,ref dark,4); }
        catch(DllNotFoundException) { } catch(EntryPointNotFoundException) { }
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if(disposing) icon.Dispose(); }
}

static class AppPreferences {
    static string FileFor(string name) { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CopyCheck",name+".txt"); }
    public static bool AutomaticUpdates { get { return AdminPreference.ReadValue(FileFor("automatic-updates"),true); } set { AdminPreference.Write(FileFor("automatic-updates"),value); } }
    public static bool Animation { get { return AdminPreference.ReadValue(FileFor("animation"),true); } set { AdminPreference.Write(FileFor("animation"),value); } }
}
sealed class ReleaseInfo {
    public Version Version;
    public string DownloadUrl, Sha256;
    public long Size;
}
static class Updates {
    public static readonly Version Current = new Version(1,0,9);
    public const string Api = "https://api.github.com/repos/aidenculpepper/copycheck/releases/latest";
    public static ReleaseInfo Available;
    public static bool Busy;
    public static string Status = "Ready to check GitHub.";
    public static event Action Changed;
    static void Notify() { if(Changed != null) Changed(); }
    internal static ReleaseInfo Parse(string json) {
        var data = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 1024*1024 }.Deserialize<System.Collections.Generic.Dictionary<string,object>>(json);
        if(Convert.ToBoolean(data["draft"]) || Convert.ToBoolean(data["prerelease"])) throw new InvalidDataException("Only published stable releases are supported.");
        string tag = (string)data["tag_name"];
        Version version;
        if(!System.Text.RegularExpressions.Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$") || !Version.TryParse(tag.Substring(1),out version)) throw new InvalidDataException("The release version is invalid.");
        if(version <= Current) return null;
        foreach(object item in (System.Collections.IEnumerable)data["assets"]) {
            var asset = (System.Collections.Generic.Dictionary<string,object>)item;
            if((string)asset["name"] != "CopyCheckSetup.exe") continue;
            string url = (string)asset["browser_download_url"];
            string expected = "https://github.com/aidenculpepper/copycheck/releases/download/"+tag+"/CopyCheckSetup.exe";
            if(url != expected) throw new InvalidDataException("The installer URL is not a CopyCheck release.");
            object digest;
            if(!asset.TryGetValue("digest",out digest) || digest == null || !System.Text.RegularExpressions.Regex.IsMatch((string)digest,@"^sha256:[a-fA-F0-9]{64}$"))
                throw new InvalidDataException("GitHub has not supplied a valid installer checksum.");
            long size = Convert.ToInt64(asset["size"]);
            if(size <= 0 || size > 100*1024*1024) throw new InvalidDataException("The installer size is invalid.");
            return new ReleaseInfo { Version=version, DownloadUrl=url, Sha256=((string)digest).Substring(7), Size=size };
        }
        throw new InvalidDataException("This release has no CopyCheckSetup.exe installer.");
    }
    static System.Net.HttpWebRequest Request(string url) {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
        request.UserAgent = "CopyCheck/"+Current;
        request.Accept = "application/vnd.github+json";
        request.Timeout = 30000; request.ReadWriteTimeout = 30000;
        return request;
    }
    public static ReleaseInfo Fetch() {
        try {
            using(var response = Request(Api).GetResponse())
            using(var reader = new StreamReader(response.GetResponseStream())) {
                var text = new System.Text.StringBuilder();
                char[] buffer = new char[4096]; int count;
                while((count=reader.Read(buffer,0,buffer.Length)) > 0) {
                    text.Append(buffer,0,count);
                    if(text.Length > 1024*1024) throw new InvalidDataException("The release response was too large.");
                }
                return Parse(text.ToString());
            }
        } catch(System.Net.WebException ex) {
            using(var response = ex.Response as System.Net.HttpWebResponse) {
                if(response != null && response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
                if(response != null && (int)response.StatusCode == 403) throw new IOException("GitHub temporarily limited requests. Try again later.",ex);
            }
            throw new IOException("Could not reach GitHub. Check your connection and try again.",ex);
        }
    }
    public static async System.Threading.Tasks.Task CheckAsync() {
        if(Busy) return;
        Busy=true; Status="Checking GitHub..."; Notify();
        try {
            Available=await System.Threading.Tasks.Task.Run((Func<ReleaseInfo>)Fetch);
            Status=Available == null ? "You're up to date." : "Version "+Available.Version+" is available.";
        } catch(Exception ex) { Available=null; Status=ex.Message; }
        finally { Busy=false; Notify(); }
    }
    internal static bool ValidHash(string file,string expected) {
        using(var hash = System.Security.Cryptography.SHA256.Create())
        using(var stream = File.OpenRead(file))
            return string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-",""),expected,StringComparison.OrdinalIgnoreCase);
    }
    public static string Download(ReleaseInfo release) {
        string directory=Path.Combine(Path.GetTempPath(),"CopyCheck-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file=Path.Combine(directory,"CopyCheckSetup.exe");
        try {
            using(var response=Request(release.DownloadUrl).GetResponse())
            using(var input=response.GetResponseStream())
            using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                byte[] buffer=new byte[65536]; int count; long total=0;
                while((count=input.Read(buffer,0,buffer.Length)) > 0) {
                    total+=count;
                    if(total > release.Size) throw new InvalidDataException("The installer size does not match GitHub.");
                    output.Write(buffer,0,count);
                }
                if(total != release.Size) throw new InvalidDataException("The installer download was incomplete.");
            }
            if(!ValidHash(file,release.Sha256)) throw new InvalidDataException("The installer checksum did not match. Nothing was installed.");
            return file;
        } catch { if(File.Exists(file)) File.Delete(file); Directory.Delete(directory); throw; }
    }
    public static void LaunchInstaller(string file) {
        using(var process=Process.Start(new ProcessStartInfo { FileName=file,UseShellExecute=true,Verb="runas",Arguments="/COPYCHECKUPDATE=1",WorkingDirectory=Path.GetDirectoryName(file) })) {
            if(process == null) throw new IOException("Windows did not start the installer.");
        }
    }
    public static int Bootstrap() {
        try {
            ReleaseInfo release=Fetch();
            if(release == null) return 0;
            LaunchInstaller(Download(release)); return 2;
        } catch(Exception ex) {
            MessageBox.Show("Could not check or download the latest CopyCheck installer.\n"+ex.Message,"CopyCheck installer",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return 1;
        }
    }
}

static class AdminPreference {
    static string PreferenceFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CopyCheck","administrator.txt"); } }
    public static bool Enabled {
        get { return Read(PreferenceFile); }
        set { Write(PreferenceFile,value); }
    }
    internal static bool Read(string file) {
        return ReadValue(file,ScheduledStartup.Installed);
    }
    internal static bool ReadValue(string file,bool defaultValue) {
        try { return File.Exists(file) ? File.ReadAllText(file).Trim() == "1" : defaultValue; }
        catch(IOException) { return false; } catch(UnauthorizedAccessException) { return false; }
    }
    internal static void Write(string file,bool value) {
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllText(file,value ? "1" : "0");
    }
}
static class Elevation {
    public static bool IsAdministrator {
        get { using(var identity = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }
    }
    internal static ProcessStartInfo LaunchInfo(int previousPid,bool showSettings,bool animationOff) {
        string arguments = previousPid > 0 ? "--wait-for " + previousPid : "";
        if(showSettings) arguments += " --show-settings";
        if(animationOff) arguments += " --animation-off";
        return new ProcessStartInfo { FileName = Application.ExecutablePath, WorkingDirectory = Application.StartupPath, UseShellExecute = true, Verb = "runas", Arguments = arguments.Trim() };
    }
    public static void Launch(int previousPid,bool showSettings,bool animationOff) {
        using(var child = Process.Start(LaunchInfo(previousPid,showSettings,animationOff))) {
            if(child == null) throw new InvalidOperationException("Windows did not start CopyCheck.");
        }
    }
}
static class VisibleStartup {
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    public static bool Enabled { get {
        using(var run=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey)) {
            if(run == null || run.GetValue("CopyCheck") == null) return ScheduledStartup.AtLogon;
        }
        using(var approval=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ApprovalKey)) {
            var state=approval == null ? null : approval.GetValue("CopyCheck") as byte[];
            return ApprovalEnabled(state);
        }
    } }
    internal static bool ApprovalEnabled(byte[] state) { return state == null || state.Length == 0 || (state[0] & 1) == 0; }
    internal static string Command(string executable) { return "\""+executable+"\" --scheduled"; }
    public static void SetEnabled(bool value) {
        using(var run=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey)) {
            if(value) run.SetValue("CopyCheck",Command(Application.ExecutablePath),Microsoft.Win32.RegistryValueKind.String);
            else run.DeleteValue("CopyCheck",false);
        }
        using(var approval=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ApprovalKey,true)) {
            if(approval != null) approval.DeleteValue("CopyCheck",false);
        }
    }
    public static void Remove() { SetEnabled(false); }
}
static class Startup {
    static string Shortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"CopyCheck.lnk"); } }
    public static bool IsEnabled { get { return ScheduledStartup.Installed ? VisibleStartup.Enabled : File.Exists(Shortcut); } }
    public static void SetEnabled(bool value) {
        if(ScheduledStartup.Installed) { ScheduledStartup.Configure(AdminPreference.Enabled,value); RemoveLegacyShortcut(); return; }
        SetShortcut(value,Shortcut,Application.ExecutablePath);
    }
    internal static void RemoveLegacyShortcut() { if(File.Exists(Shortcut)) File.Delete(Shortcut); }
    internal static void SetShortcut(bool value,string shortcut,string executable) {
        if(!value) { if(File.Exists(shortcut)) File.Delete(shortcut); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(shortcut));
        object shell = null, link = null;
        try {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            link = shell.GetType().InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[] { shortcut });
            var type = link.GetType();
            type.InvokeMember("TargetPath",System.Reflection.BindingFlags.SetProperty,null,link,new object[] { executable });
            type.InvokeMember("WorkingDirectory",System.Reflection.BindingFlags.SetProperty,null,link,new object[] { Path.GetDirectoryName(executable) });
            type.InvokeMember("Description",System.Reflection.BindingFlags.SetProperty,null,link,new object[] { "CopyCheck" });
            type.InvokeMember("Save",System.Reflection.BindingFlags.InvokeMethod,null,link,null);
        } finally {
            if(link != null) Marshal.FinalReleaseComObject(link);
            if(shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
sealed class ComObjects : IDisposable {
    readonly System.Collections.Generic.List<object> objects = new System.Collections.Generic.List<object>();
    public dynamic Keep(object value) { if(value != null && Marshal.IsComObject(value)) objects.Add(value); return value; }
    public void Dispose() { for(int i = objects.Count-1; i >= 0; i--) Marshal.ReleaseComObject(objects[i]); }
}
static class ScheduledStartup {
    public static bool Installed {
        get {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return !string.IsNullOrEmpty(programFiles) &&
                string.Equals(Path.GetFullPath(Application.StartupPath).TrimEnd('\\'),Path.Combine(programFiles,"CopyCheck"),StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(Application.StartupPath,"installed.flag"));
        }
    }
    static string Sid { get { using(var identity = WindowsIdentity.GetCurrent()) return identity.User.Value; } }
    static string Name { get { return "CopyCheck-"+Sid; } }
    static dynamic Connect(ComObjects scope) {
        dynamic service = scope.Keep(Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")));
        service.Connect(); return service;
    }
    public static bool HasTask { get {
        using(var scope=new ComObjects()) {
            dynamic service=Connect(scope), folder=scope.Keep(service.GetFolder("\\"));
            return TaskExists(folder,Name,scope);
        }
    } }
    internal static bool TaskExists(dynamic folder,string name,ComObjects scope) {
        try { scope.Keep(folder.GetTask(name)); return true; }
        catch(Exception ex) {
            // COM interop maps ERROR_FILE_NOT_FOUND to FileNotFoundException,
            // not necessarily COMException. A missing task is normal on first install.
            if((ex is COMException || ex is FileNotFoundException || ex is DirectoryNotFoundException) &&
               (ex.HResult == unchecked((int)0x80070002) || ex.HResult == unchecked((int)0x80070003))) return false;
            throw;
        }
    }
    public static bool AtLogon { get { return ReadState(false); } }
    public static bool Highest { get { return ReadState(true); } }
    static bool ReadState(bool highest) {
        try {
            using(var scope = new ComObjects()) {
                dynamic service = Connect(scope), folder = scope.Keep(service.GetFolder("\\")), task = scope.Keep(folder.GetTask(Name));
                dynamic definition = scope.Keep(task.Definition);
                if(highest) { dynamic principal = scope.Keep(definition.Principal); return (int)principal.RunLevel == 1; }
                dynamic triggers = scope.Keep(definition.Triggers);
                for(int i=1;i<=(int)triggers.Count;i++) { dynamic trigger = scope.Keep(triggers.Item(i)); if((int)trigger.Type == 9 && (bool)trigger.Enabled) return true; }
            }
        } catch(COMException) { } catch(IOException) { } catch(UnauthorizedAccessException) { }
        return false;
    }
    internal static string DefinitionXml(bool administrator,bool startup,string executable,string sid) {
        var escape = new Func<string,string>(System.Security.SecurityElement.Escape);
        return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>"+
            "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">"+
            "<RegistrationInfo><Description>CopyCheck tray app for this user</Description></RegistrationInfo>"+
            "<Triggers><LogonTrigger><Enabled>"+(startup ? "true" : "false")+"</Enabled><UserId>"+escape(sid)+"</UserId></LogonTrigger></Triggers>"+
            "<Principals><Principal id=\"User\"><UserId>"+escape(sid)+"</UserId><LogonType>InteractiveToken</LogonType><RunLevel>"+(administrator ? "HighestAvailable" : "LeastPrivilege")+"</RunLevel></Principal></Principals>"+
            "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><ExecutionTimeLimit>PT0S</ExecutionTimeLimit></Settings>"+
            "<Actions Context=\"User\"><Exec><Command>"+escape(executable)+"</Command><Arguments>--scheduled</Arguments><WorkingDirectory>"+escape(Path.GetDirectoryName(executable))+"</WorkingDirectory></Exec></Actions></Task>";
    }
    public static void Configure(bool administrator,bool startup) {
        if(!Installed) throw new InvalidOperationException("CopyCheck must be installed in Program Files\\CopyCheck to configure scheduled startup.");
        using(var scope = new ComObjects()) {
            dynamic service = Connect(scope), folder = scope.Keep(service.GetFolder("\\"));
            scope.Keep(folder.RegisterTask(Name,DefinitionXml(administrator,false,Application.ExecutablePath,Sid),6,Sid,null,3,null));
        }
        VisibleStartup.SetEnabled(startup);
    }
    public static void CheckInstallerAccount() {
        uint pid;
        Native.GetWindowThreadProcessId(Native.GetShellWindow(),out pid);
        IntPtr process = Native.OpenProcess(0x1000,false,pid), token = IntPtr.Zero;
        if(process == IntPtr.Zero) throw new InvalidOperationException("Could not identify the signed-in desktop user.");
        try {
            if(!Native.OpenProcessToken(process,8,out token)) throw new System.ComponentModel.Win32Exception();
            using(var identity = new WindowsIdentity(token)) {
                if(identity.User.Value != Sid) throw new InvalidOperationException("Install while signed into an administrator Windows account. Using another account's credentials cannot provide elevated startup in your desktop session.");
            }
        } finally { if(token != IntPtr.Zero) Native.CloseHandle(token); Native.CloseHandle(process); }
    }
    public static bool TryLaunch() {
        if(!Installed) return false;
        try {
            using(var scope = new ComObjects()) {
                dynamic service = Connect(scope), folder = scope.Keep(service.GetFolder("\\")), task = scope.Keep(folder.GetTask(Name));
                dynamic definition = scope.Keep(task.Definition), principal = scope.Keep(definition.Principal);
                if((int)principal.RunLevel != 1) return false;
                scope.Keep(task.Run(null)); return true;
            }
        } catch(COMException) { return false; } catch(IOException) { return false; } catch(UnauthorizedAccessException) { return false; }
    }
    public static void RemoveAllInstalledTasks() {
        VisibleStartup.Remove();
        Startup.RemoveLegacyShortcut();
        using(var scope = new ComObjects()) {
            dynamic service = Connect(scope), folder = scope.Keep(service.GetFolder("\\")), tasks = scope.Keep(folder.GetTasks(1));
            var names = new System.Collections.Generic.List<string>();
            for(int i=1;i<=(int)tasks.Count;i++) {
                dynamic task = scope.Keep(tasks.Item(i));
                if(!((string)task.Name).StartsWith("CopyCheck-S-1-",StringComparison.Ordinal)) continue;
                dynamic definition = scope.Keep(task.Definition), actions = scope.Keep(definition.Actions);
                if((int)actions.Count != 1) continue;
                dynamic action = scope.Keep(actions.Item(1));
                if((int)action.Type == 0 && string.Equals((string)action.Path,Application.ExecutablePath,StringComparison.OrdinalIgnoreCase)) names.Add((string)task.Name);
            }
            foreach(string name in names) folder.DeleteTask(name,0);
        }
    }
}
class Listener : Form {
    readonly CopyContext context;
    public Listener(CopyContext c) { context = c; var h = Handle; if(!Native.AddClipboardFormatListener(h)) throw new System.ComponentModel.Win32Exception(); }
    protected override void SetVisibleCore(bool value) { base.SetVisibleCore(false); }
    protected override void WndProc(ref Message m) { if(m.Msg == 0x31D) context.ClipboardChanged(); base.WndProc(ref m); }
    protected override void Dispose(bool disposing) { if(IsHandleCreated) Native.RemoveClipboardFormatListener(Handle); base.Dispose(disposing); }
}
class CheckOverlay : Form {
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly DateTime started = DateTime.UtcNow;
    readonly float scale;
    public CheckOverlay(Point anchor) {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        uint dpi = 96;
        try { dpi = Native.GetDpiForWindow(Native.GetForegroundWindow()); } catch(EntryPointNotFoundException) { }
        scale = Math.Max(1, dpi/96f);
        int size = (int)Math.Ceiling(22*scale);
        ClientSize = new Size(size,size);
        var area = Screen.FromPoint(anchor).WorkingArea;
        Location = new Point(Math.Max(area.Left, Math.Min(area.Right-size, anchor.X+(int)(4*scale))), Math.Max(area.Top, Math.Min(area.Bottom-size, anchor.Y-size/2)));
        timer.Interval = 16;
        timer.Tick += delegate {
            double elapsed = (DateTime.UtcNow-started).TotalMilliseconds;
            if(elapsed >= 700) { Close(); return; }
            Render(elapsed);
        };
        Render(0); timer.Start();
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x20 | 0x80 | 0x80000; return p; } }
    void Render(double elapsed) {
      using(var bitmap = new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb))
      using(var graphics = Graphics.FromImage(bitmap)) {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.ScaleTransform(scale,scale);
        using(var pen = new Pen(Color.FromArgb(25,177,106),2.3f)) {
            pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            float progress = Math.Min(1,(float)elapsed/170);
            PointF a = new PointF(4,11), b = new PointF(9,16), c = new PointF(18,6);
            if(progress < .4f) graphics.DrawLine(pen,a,new PointF(a.X+(b.X-a.X)*progress/.4f,a.Y+(b.Y-a.Y)*progress/.4f));
            else { graphics.DrawLine(pen,a,b); float t = (progress-.4f)/.6f; graphics.DrawLine(pen,b,new PointF(b.X+(c.X-b.X)*t,b.Y+(c.Y-b.Y)*t)); }
        }
        IntPtr dc = Native.GetDC(IntPtr.Zero), memory = Native.CreateCompatibleDC(dc);
        IntPtr image = bitmap.GetHbitmap(Color.FromArgb(0)), previous = Native.SelectObject(memory,image);
        try {
            var destination = new Native.Position { x = Left, y = Top };
            var origin = new Native.Position(); var size = new Native.Position { x = Width, y = Height };
            var blend = new Native.Blend { alpha = (byte)(elapsed < 450 ? 255 : Math.Max(0,255*(700-elapsed)/250)), format = 1 };
            Native.UpdateLayeredWindow(Handle,dc,ref destination,ref size,memory,ref origin,0,ref blend,2);
        } finally { Native.SelectObject(memory,previous); Native.DeleteObject(image); Native.DeleteDC(memory); Native.ReleaseDC(IntPtr.Zero,dc); }
      }
    }
    protected override void Dispose(bool disposing) { if(disposing) timer.Dispose(); base.Dispose(disposing); }
}
static class Native {
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("kernel32.dll",SetLastError=true)] public static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("advapi32.dll",SetLastError=true)] public static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    [StructLayout(LayoutKind.Sequential)] public struct Position { public int x,y; }
    [StructLayout(LayoutKind.Sequential, Pack=1)] public struct Blend { public byte operation,flags,alpha,format; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr image);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr image);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr window,IntPtr dc,ref Position destination,ref Position size,IntPtr source,ref Position origin,uint key,ref Blend blend,uint flags);
    public delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct Keyboard { public uint vk,scan,flags,time; public IntPtr extra; }
    [DllImport("user32.dll", SetLastError=true)] public static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
    [DllImport("kernel32.dll", CharSet=CharSet.Auto)] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] public static extern int CountClipboardFormats();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool AddClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr window);
}
}
