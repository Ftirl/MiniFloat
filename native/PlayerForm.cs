using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MiniFloat {
internal sealed class Preferences {
    public int LongSide { get; set; }
    public int OpacityPercent { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Preferences() { LongSide=240; OpacityPercent=85; X=Int32.MinValue; Y=Int32.MinValue; }
    static string PathName { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.json"); } }
    internal static Preferences Load() {
        try { return new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(PathName)) ?? new Preferences(); }
        catch { return new Preferences(); }
    }
    internal void Save() {
        try { File.WriteAllText(PathName,new JavaScriptSerializer().Serialize(this)); } catch { /* Read-only install location. */ }
    }
}

internal sealed class PlayerForm : Form {
    IntPtr source, thumbnail;
    Native.RECT original;
    uint sourcePid;
    long sourceStyle;
    uint sourceColor, sourceAlphaFlags;
    byte sourceAlpha;
    bool sourceHadAlpha;
    double ratio;
    bool tucked, clickThrough, cleanupDone;
    readonly bool persist;
    readonly Preferences preferences;
    readonly Timer monitor=new Timer();
    readonly NotifyIcon tray=new NotifyIcon();
    readonly Action<string> command;
    internal bool HotkeyRegistered { get; private set; }
    internal IntPtr Source { get { return source; } }
    internal PlayerForm(IntPtr source,double width,double height,Action<string> command,bool persist) {
        this.source=source; this.command=command; this.persist=persist;
        ratio=Geometry.SafeRatio(width,height);
        preferences=persist?Preferences.Load():new Preferences();
        Native.GetWindowRect(source,out original);
        Native.GetWindowThreadProcessId(source,out sourcePid);
        sourceStyle=Native.GetWindowLongPtr(source,Native.GWL_EXSTYLE).ToInt64();
        sourceHadAlpha=Native.GetLayeredWindowAttributes(source,out sourceColor,out sourceAlpha,out sourceAlphaFlags);
        Text="微窗 · MiniFloat"; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false;
        AutoScaleMode=AutoScaleMode.None; BackColor=Color.Black; TopMost=true; KeyPreview=true;
        StartPosition=FormStartPosition.Manual;
        Size=Geometry.SizeFor(preferences.LongSide,ratio);
        Rectangle area=Screen.FromPoint(new Point(original.Left,original.Top)).WorkingArea;
        Location=new Point(area.Right-Width-24,area.Bottom-Height-24);
        if (preferences.X!=Int32.MinValue) {
            var saved=new Rectangle(preferences.X,preferences.Y,Width,Height);
            foreach (var screen in Screen.AllScreens) if (screen.WorkingArea.IntersectsWith(saved)) {
                Location=new Point(Math.Max(screen.WorkingArea.Left,Math.Min(screen.WorkingArea.Right-Width,saved.X)),Math.Max(screen.WorkingArea.Top,Math.Min(screen.WorkingArea.Bottom-Height,saved.Y))); break;
            }
        }
        Opacity=Math.Max(.1,Math.Min(1,preferences.OpacityPercent/100.0));
        ContextMenuStrip=BuildMenu();
        tray.Icon=SystemIcons.Application; tray.Text="微窗 · 右键设置，双击恢复可见";
        tray.ContextMenuStrip=BuildMenu(); tray.DoubleClick+=delegate { Recover(); }; tray.Visible=true;
        monitor.Interval=500; monitor.Tick+=delegate {
            uint pid; Native.GetWindowThreadProcessId(source,out pid);
            if (!Native.IsWindow(source) || pid!=sourcePid) { Close(); return; }
            if (Native.IsIconic(source)) { RestoreSource(); return; }
            UpdateThumbnail();
        };
        Resize+=delegate { UpdateThumbnail(); };
        MouseWheel+=delegate(object sender,MouseEventArgs e) {
            if ((ModifierKeys & Keys.Control)!=0) SetOpacity((int)Math.Round(Opacity*100)+(e.Delta>0?5:-5));
            else SetLongSide((int)Math.Round(Math.Max(Width,Height)*(e.Delta>0?1.08:1/1.08)));
        };
        MouseDoubleClick+=delegate(object sender,MouseEventArgs e) { if(e.Button==MouseButtons.Left) command("toggle"); };
        KeyDown+=delegate(object sender,KeyEventArgs e) {
            if(e.KeyCode==Keys.Space) { command("toggle"); e.Handled=true; }
            else if(e.KeyCode==Keys.Left) command("back");
            else if(e.KeyCode==Keys.Right) command("forward");
            else if(e.KeyCode==Keys.Escape) Close();
        };
    }
    protected override CreateParams CreateParams {
        get { var value=base.CreateParams; value.ExStyle|=0x80; return value; }
    }
    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        int result=Native.DwmRegisterThumbnail(Handle,source,out thumbnail);
        if(result!=0) throw new InvalidOperationException("无法创建桌面视频预览，错误 0x"+result.ToString("X8"));
        UpdateThumbnail();
        HotkeyRegistered=Native.RegisterHotKey(Handle,1,0x4003,0x4F); // Ctrl+Alt+O, no repeat
        TuckSource(); monitor.Start();
    }
    internal void SetRatio(double width,double height) { ratio=Geometry.SafeRatio(width,height); SetLongSide(Math.Max(Width,Height)); }
    internal void SetLongSide(int length) {
        Size=Geometry.SizeFor(length,ratio); UpdateThumbnail();
    }
    internal void SetOpacity(int percent) {
        Opacity=Math.Max(10,Math.Min(100,percent))/100.0;
        if(clickThrough) SetClickThrough(true);
    }
    internal void Recover() {
        SetClickThrough(false); SetOpacity(100);
        Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;
        Location=new Point(Math.Max(area.Left,Math.Min(area.Right-Width,Left)),Math.Max(area.Top,Math.Min(area.Bottom-Height,Top)));
        Show(); Activate();
    }
    internal void SetClickThrough(bool enabled) {
        clickThrough=enabled;
        long style=Native.GetWindowLongPtr(Handle,Native.GWL_EXSTYLE).ToInt64();
        if(enabled) style|=Native.WS_EX_TRANSPARENT|0x80000; else style&=~Native.WS_EX_TRANSPARENT;
        Native.SetWindowLongPtr(Handle,Native.GWL_EXSTYLE,new IntPtr(style));
        if(enabled) Native.SetLayeredWindowAttributes(Handle,0,(byte)Math.Round(Opacity*255),2);
    }
    internal void TuckSource() {
        if(tucked || !Native.IsWindow(source)) return;
        // A fully offscreen GDI surface may stop repainting. Keep it composed on-screen
        // at 1/255 alpha, with input passing through. The DWM replica has its own alpha.
        Native.SetWindowLongPtr(source,Native.GWL_EXSTYLE,new IntPtr(sourceStyle|0x80000|Native.WS_EX_TRANSPARENT));
        if(!Native.SetLayeredWindowAttributes(source,0,1,2)) {
            Native.SetWindowLongPtr(source,Native.GWL_EXSTYLE,new IntPtr(sourceStyle));
            throw new InvalidOperationException("无法收起 浏览器原小窗。");
        }
        tucked=true;
    }
    internal void RestoreSource() {
        if(!tucked) return;
        uint pid; Native.GetWindowThreadProcessId(source,out pid);
        if(Native.IsWindow(source) && pid==sourcePid) {
            if(sourceHadAlpha) Native.SetLayeredWindowAttributes(source,sourceColor,sourceAlpha,sourceAlphaFlags);
            else Native.SetLayeredWindowAttributes(source,0,255,2);
            Native.SetWindowLongPtr(source,Native.GWL_EXSTYLE,new IntPtr(sourceStyle));
            Native.SetWindowPos(source,IntPtr.Zero,original.Left,original.Top,original.Width,original.Height,Native.SWP_NOACTIVATE|Native.SWP_NOZORDER);
        }
        tucked=false;
    }
    void UpdateThumbnail() {
        if(thumbnail==IntPtr.Zero || ClientSize.Width<1 || ClientSize.Height<1) return;
        Native.RECT client; Native.GetClientRect(source,out client);
        if(client.Width<1 || client.Height<1) return;
        var props=new Native.THUMBNAIL {
            Flags=1|2|4|8|16,
            Destination=new Native.RECT(0,0,ClientSize.Width,ClientSize.Height),
            Source=Geometry.FitSource(client.Width,client.Height,ratio),
            Opacity=255, Visible=true, ClientOnly=true
        };
        int result=Native.DwmUpdateThumbnailProperties(thumbnail,ref props);
        if(result!=0) Text="微窗 · 预览暂不可用";
    }
    ContextMenuStrip BuildMenu() {
        var menu=new ContextMenuStrip { ShowImageMargin=false, Font=new Font("Microsoft YaHei UI",9) };
        var status=new ToolStripMenuItem("微窗") { Enabled=false };
        menu.Items.Add(status); menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("播放 / 暂停    空格",null,delegate { command("toggle"); });
        menu.Items.Add("静音 / 取消静音",null,delegate { command("mute"); });
        var sizes=new ToolStripMenuItem("尺寸（长边）");
        foreach(int length in new[]{64,80,96,120,180,240,360,480,720}) {
            int value=length; sizes.DropDownItems.Add(value+" px",null,delegate { SetLongSide(value); });
        }
        menu.Items.Add(sizes);
        var opacity=new ToolStripMenuItem("不透明度");
        var slider=new TrackBar { Minimum=10, Maximum=100, TickFrequency=10, SmallChange=5, LargeChange=10, Width=210, Height=45, AutoSize=false };
        slider.ValueChanged+=delegate { SetOpacity(slider.Value); };
        opacity.DropDownItems.Add(new ToolStripControlHost(slider) { AutoSize=false, Size=new Size(220,52) });
        foreach(int percent in new[]{10,25,50,75,100}) {
            int value=percent; opacity.DropDownItems.Add(value+"%",null,delegate { SetOpacity(value); });
        }
        menu.Items.Add(opacity);
        var through=new ToolStripMenuItem("鼠标穿透（托盘可恢复）");
        through.Click+=delegate { SetClickThrough(!clickThrough); };
        menu.Items.Add(through);
        var tuck=new ToolStripMenuItem("收起 浏览器原小窗");
        tuck.Click+=delegate { if(tucked) RestoreSource(); else TuckSource(); };
        menu.Items.Add(tuck);
        menu.Items.Add("恢复可见    Ctrl+Alt+O",null,delegate { Recover(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("返回视频网页",null,delegate { command("return"); });
        menu.Items.Add("关闭小窗    Esc",null,delegate { Close(); });
        menu.Opening+=delegate {
            status.Text=Width+" × "+Height+"  ·  不透明度 "+Math.Round(Opacity*100)+"%";
            slider.Value=(int)Math.Round(Opacity*100); through.Checked=clickThrough; tuck.Checked=tucked;
        };
        return menu;
    }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x312 && m.WParam.ToInt32()==1) { Recover(); return; }
        if(m.Msg==0x24) {
            base.WndProc(ref m);
            var limits=(Native.MINMAXINFO)Marshal.PtrToStructure(m.LParam,typeof(Native.MINMAXINFO));
            Size minimum=Geometry.SizeFor(64,ratio);
            limits.MinTrackSize.X=minimum.Width; limits.MinTrackSize.Y=minimum.Height;
            limits.MaxTrackSize.X=2400; limits.MaxTrackSize.Y=2400;
            Marshal.StructureToPtr(limits,m.LParam,false); return;
        }
        if(m.Msg==0x214) {
            var rect=(Native.RECT)Marshal.PtrToStructure(m.LParam,typeof(Native.RECT));
            rect=Geometry.AspectRect(rect,m.WParam.ToInt32(),ratio);
            Marshal.StructureToPtr(rect,m.LParam,false); m.Result=new IntPtr(1); return;
        }
        if(m.Msg==0x84) {
            long packed=m.LParam.ToInt64();
            Point p=PointToClient(new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff)));
            int border=Math.Min(5,Math.Min(Width,Height)/5);
            bool l=p.X<border,r=p.X>=Width-border,t=p.Y<border,b=p.Y>=Height-border;
            int hit=t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:1;
            m.Result=new IntPtr(hit); return;
        }
        base.WndProc(ref m);
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        // Defer drag to MouseMove, preserving double-click and right-click handling.
        if(e.Button==MouseButtons.Left) dragStart=e.Location;
    }
    Point dragStart;
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if(e.Button==MouseButtons.Left && (Math.Abs(e.X-dragStart.X)>3 || Math.Abs(e.Y-dragStart.Y)>3)) {
            Native.ReleaseCapture(); Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);
        }
    }
    protected override void OnFormClosed(FormClosedEventArgs e) {
        Cleanup(); base.OnFormClosed(e);
    }
    internal void Cleanup() {
        if(cleanupDone) return; cleanupDone=true;
        monitor.Stop(); monitor.Dispose();
        if(HotkeyRegistered) Native.UnregisterHotKey(Handle,1);
        RestoreSource();
        if(thumbnail!=IntPtr.Zero) { Native.DwmUnregisterThumbnail(thumbnail); thumbnail=IntPtr.Zero; }
        tray.Visible=false; tray.Dispose();
        if(persist) { preferences.LongSide=Math.Max(Width,Height); preferences.OpacityPercent=(int)Math.Round(Opacity*100); preferences.X=Left; preferences.Y=Top; preferences.Save(); }
    }
}
}
