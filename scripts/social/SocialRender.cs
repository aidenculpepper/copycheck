// Draws the real CopyCheck settings window and app icon to PNG files for the
// share images. Compiled together with CopyCheck.cs by Build-SocialImages.ps1,
// with this class as the entry point, so it never installs the keyboard hook,
// the tray icon or the clipboard listener.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace CopyCheck {
static class SocialRender {
    [STAThread] static int Main(string[] args) {
        string output = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(output);
        Native.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // The status a successful check shows when this build is the latest release.
        Updates.Status = "You're up to date.";
        using(var form = new SettingsForm(true)) {
            // Controls report Visible=false until their window is shown, so show it
            // far off-screen for the moment it takes to draw.
            form.ShowInTaskbar = false;
            form.Location = new Point(-20000,-20000);
            form.Show();
            Application.DoEvents();
            using(var bitmap = new Bitmap(form.ClientSize.Width,form.ClientSize.Height)) {
                using(var g = Graphics.FromImage(bitmap)) g.Clear(form.BackColor);
                // Draw each control at its own bounds so the image is the client area
                // only, without the window frame.
                for(int i = form.Controls.Count-1; i >= 0; i--) {
                    var control = form.Controls[i];
                    if(!control.Visible) continue;
                    control.DrawToBitmap(bitmap,control.Bounds);
                }
                bitmap.Save(Path.Combine(output,"settings-window.png"),ImageFormat.Png);
            }
        }
        using(var icon = Brand.CreateBitmap(256)) icon.Save(Path.Combine(output,"app-icon.png"),ImageFormat.Png);
        return 0;
    }
}
}
