using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace MiniFloat {
internal static class Program {
    [STAThread] static int Main(string[] args) {
        if(args.Length>0 && args[0]=="--self-test") return Tests.Run(args.Length>1?args[1]:"test-results.txt");
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length>0 && (args[0]=="--demo" || args[0]=="--visual-test")) {
            return Demo.Run(args[0]=="--visual-test",args.Length>1?args[1]:AppDomain.CurrentDomain.BaseDirectory);
        }
        if(args.Length==0 || !args[0].StartsWith("chrome-extension://",StringComparison.Ordinal)) {
            MessageBox.Show("请从 Chrome 或 Edge 的“微窗”扩展调用。\n\n首次使用先运行 Install.cmd，再在 Chrome 或 Edge 加载 extension 文件夹。\n\n运行 Demo.cmd 可预览小窗操作。","微窗"); return 0;
        }
        bool created;
        using(var mutex=new Mutex(true,"Local\\MiniFloat.Host.CurrentUser."+Environment.UserName,out created)) {
            if(!created) {
                try { Protocol.Write(Console.OpenStandardOutput(),new {type="error",error="另一个微窗助手正在运行，请先关闭它再重试。"}); } catch { }
                return 1;
            }
            try {
                using(var host=new HostContext()) {
                    Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e) { host.Fail(e.Exception.Message); };
                    Application.Run(host);
                }
            } catch { return 1; }
        }
        return 0;
    }
}

internal sealed class HostContext : ApplicationContext {
    readonly Control dispatch=new Control();
    readonly Stream output=Console.OpenStandardOutput();
    readonly object outputLock=new object();
    readonly System.Windows.Forms.Timer finder=new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer idle=new System.Windows.Forms.Timer();
    readonly int browserPid;
    HashSet<IntPtr> before;
    PlayerForm player;
    string attachId;
    double width,height;
    DateTime deadline;
    IntPtr previousCandidate;
    bool shuttingDown,suppressClose;
    internal HostContext() {
        browserPid=Native.BrowserAncestor();
        var handle=dispatch.Handle;
        finder.Interval=100; finder.Tick+=FindWindow;
        idle.Interval=60000; idle.Tick+=delegate { if(player==null && !finder.Enabled) ExitThread(); };
        idle.Start();
        var reader=new Thread(ReadLoop) { IsBackground=true, Name="MiniFloat native messaging" }; reader.Start();
    }
    void ReadLoop() {
        try {
            using(var input=Console.OpenStandardInput()) {
                Dictionary<string,object> message;
                while((message=Protocol.Read(input))!=null) {
                    var copy=message;
                    dispatch.BeginInvoke((Action)delegate { Handle(copy); });
                }
            }
        } catch { /* EOF or malformed input ends the session and restores the source. */ }
        try { dispatch.BeginInvoke((Action)delegate { ExitThread(); }); } catch { }
    }
    void Send(object value) {
        if(shuttingDown) return;
        try { lock(outputLock) Protocol.Write(output,value); }
        catch { ExitThread(); }
    }
    void Handle(Dictionary<string,object> message) {
        string id=Protocol.Text(message,"id");
        try {
            switch(Protocol.Text(message,"type")) {
                case "prepare":
                    if(browserPid==0) throw new InvalidOperationException("无法确认调用此助手的 浏览器进程。");
                    ClosePlayer(false);
                    before=new HashSet<IntPtr>(Native.BrowserWindows(browserPid,false));
                    idle.Stop(); idle.Start();
                    Send(new {id=id,ok=true}); break;
                case "attach":
                    if(before==null) throw new InvalidOperationException("请先准备视频窗口。");
                    width=Protocol.Number(message,"width",0); height=Protocol.Number(message,"height",0);
                    if(width<=0 || height<=0 || width>65536 || height>65536) throw new InvalidOperationException("视频尺寸无效。");
                    attachId=id; deadline=DateTime.UtcNow.AddSeconds(10); previousCandidate=IntPtr.Zero;
                    finder.Start(); break;
                case "ratio":
                    if(player!=null) player.SetRatio(Protocol.Number(message,"width",16),Protocol.Number(message,"height",9));
                    break;
                case "close":
                    ClosePlayer(false); Send(new {id=id,ok=true}); idle.Stop(); idle.Start(); break;
                default: throw new InvalidOperationException("不支持的消息类型。");
            }
        } catch(Exception error) { Send(new {id=id,error=error.Message}); }
    }
    void FindWindow(object sender,EventArgs e) {
        var candidates=Native.BrowserWindows(browserPid,true).Where(hwnd=>!before.Contains(hwnd)).ToArray();
        if(candidates.Length==1 && candidates[0]==previousCandidate) {
            finder.Stop();
            try {
                player=new PlayerForm(candidates[0],width,height,action=>Send(new {type="control",action=action}),true);
                player.FormClosed+=delegate {
                    player=null;
                    if(!suppressClose) Send(new {type="closed"});
                    idle.Stop(); idle.Start();
                };
                player.Show();
                Send(new {id=attachId,ok=true,hotkey=player.HotkeyRegistered});
            } catch(Exception error) { ClosePlayer(false); Send(new {id=attachId,error=error.Message}); }
            return;
        }
        previousCandidate=candidates.Length==1?candidates[0]:IntPtr.Zero;
        if(DateTime.UtcNow>deadline) {
            finder.Stop();
            Send(new {id=attachId,error="没有唯一识别到新创建的 浏览器画中画窗口。请关闭其他画中画窗口后重试。"});
        }
    }
    void ClosePlayer(bool notify) {
        finder.Stop(); suppressClose=!notify;
        if(player!=null) { player.Cleanup(); player.Close(); player=null; }
        suppressClose=false;
    }
    internal void Fail(string message) {
        Send(new {id=attachId,error=message}); ClosePlayer(true); ExitThread();
    }
    protected override void ExitThreadCore() {
        if(shuttingDown) return;
        shuttingDown=true; ClosePlayer(false); base.ExitThreadCore();
    }
    protected override void Dispose(bool disposing) {
        if(disposing) { ClosePlayer(false); finder.Dispose(); idle.Dispose(); dispatch.Dispose(); output.Dispose(); }
        base.Dispose(disposing);
    }
}
}
