using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace MiniFloat {
internal sealed class DemoSource : Form {
    internal bool Alternate;
    internal DemoSource() {
        Text="MiniFloat test source"; FormBorderStyle=FormBorderStyle.None;
        AutoScaleMode=AutoScaleMode.None; ClientSize=new Size(640,360); StartPosition=FormStartPosition.Manual;
        Location=new Point(50,50); DoubleBuffered=true;
    }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(Alternate?Color.FromArgb(30,80,220):Color.FromArgb(220,40,40));
        using(var font=new Font("Segoe UI",32,FontStyle.Bold)) e.Graphics.DrawString("MiniFloat",font,Brushes.White,32,28);
        using(var font=new Font("Microsoft YaHei UI",16)) e.Graphics.DrawString("拖动移动 · 滚轮缩放\nCtrl + 滚轮调节不透明度\n右键打开菜单",font,Brushes.White,32,108);
    }
}
internal static class Demo {
    internal static int Run(bool test,string directory) {
        var source=new DemoSource(); source.Show(); Native.ShowWindow(source.Handle,5); source.Refresh();
        var backdrop=new Form { FormBorderStyle=FormBorderStyle.None, BackColor=Color.White, StartPosition=FormStartPosition.Manual, Location=new Point(80,80), Size=new Size(800,500), ShowInTaskbar=false };
        if(test) backdrop.Show();
        PlayerForm player=null;
        player=new PlayerForm(source.Handle,640,360,delegate(string action) { if(action=="return") player.Close(); else { source.Alternate=!source.Alternate; source.Refresh(); } },false);
        source.FormClosed+=delegate { if(!player.IsDisposed) player.Close(); };
        var timer=new Timer { Interval=800 }; int step=0; int exitCode=0;
        Color red=Color.Empty,blue=Color.Empty,translucent=Color.Empty;
        player.Shown+=delegate {
            if(test) { player.Location=new Point(160,160); player.SetLongSide(240); player.SetOpacity(100); }
            timer.Start();
        };
        timer.Tick+=delegate {
            if(!test) { source.Alternate=!source.Alternate; source.Refresh(); return; }
            try {
                if(step==0) { Capture(player,directory,"01-opaque.png"); red=Sample(player); source.Alternate=true; source.Refresh(); }
                if(step==1) {
                    Capture(player,directory,"02-live-tucked.png"); blue=Sample(player);
                    if(!(red.R>150 && red.B<100 && blue.B>150 && blue.R<100)) throw new Exception("Live DWM rendering failed: "+red+" -> "+blue);
                    player.SetOpacity(40);
                }
                if(step==2) {
                    Capture(player,directory,"03-opacity40.png"); translucent=Sample(player);
                    if(!(translucent.R>blue.R+50 && translucent.B>=blue.B-20)) throw new Exception("Desktop transparency failed: "+translucent);
                    player.SetLongSide(96);
                }
                if(step==3) {
                    Capture(player,directory,"04-small96.png");
                    if(player.Width!=96 || player.Height!=54) throw new Exception("Small size mismatch");
                    player.SetLongSide(64);
                }
                if(step==4) {
                    Capture(player,directory,"05-small64.png");
                    if(player.Width!=64 || player.Height!=36) throw new Exception("Minimum size mismatch");
                    player.SetOpacity(100); player.SetClickThrough(true);
                    long exStyle=Native.GetWindowLongPtr(player.Handle,Native.GWL_EXSTYLE).ToInt64();
                    if((exStyle & 0x80020)!=0x80020) throw new Exception("Click-through flags missing at 100% opacity");
                    player.SetOpacity(70); player.Recover();
                    exStyle=Native.GetWindowLongPtr(player.Handle,Native.GWL_EXSTYLE).ToInt64();
                    if((exStyle & 0x20)!=0 || player.Opacity!=1) throw new Exception("Recover failed");
                    player.RestoreSource(); Native.RECT rect; Native.GetWindowRect(source.Handle,out rect);
                    if(rect.Left!=50 || rect.Top!=50) throw new Exception("Source restore failed");
                    if((Native.GetWindowLongPtr(source.Handle,Native.GWL_EXSTYLE).ToInt64() & 0x80020)!=0) throw new Exception("Original source style not restored");
                    File.WriteAllText(Path.Combine(directory,"visual-results.txt"),"PASS: live DWM with tucked source; 40% opacity over white desktop window; 96x54 and 64x36 sizing; click-through/recovery flags; original source position and styles restored.\r\nColors: "+red+" -> "+blue+" -> "+translucent);
                    player.Close();
                }
                step++;
            } catch(Exception error) {
                File.WriteAllText(Path.Combine(directory,"visual-results.txt"),"FAIL: "+error); exitCode=1; player.Close();
            }
        };
        try { Application.Run(player); }
        finally { timer.Dispose(); player.Cleanup(); source.Dispose(); backdrop.Dispose(); }
        return exitCode;
    }
    static Color Sample(PlayerForm player) {
        using(var bitmap=new Bitmap(1,1)) {
            using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(player.Left+player.Width*3/4,player.Top+player.Height*3/4,0,0,new Size(1,1));
            return bitmap.GetPixel(0,0);
        }
    }
    static void Capture(PlayerForm player,string directory,string name) {
        Directory.CreateDirectory(directory);
        using(var bitmap=new Bitmap(player.Width+32,player.Height+32)) {
            using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(player.Left-16,player.Top-16,0,0,bitmap.Size);
            bitmap.Save(Path.Combine(directory,name),ImageFormat.Png);
        }
    }
}
}
