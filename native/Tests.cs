using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace MiniFloat {
internal static class Tests {
    static int assertions;
    static void Check(bool result,string message) { assertions++; if(!result) throw new Exception(message); }
    sealed class Fragmented : MemoryStream {
        internal Fragmented(byte[] data):base(data) { }
        public override int Read(byte[] b,int offset,int count) { return base.Read(b,offset,Math.Min(count,1)); }
    }
    static bool Rejects(byte[] data) {
        try { Protocol.Read(new MemoryStream(data)); return false; } catch { return true; }
    }
    internal static int Run(string path) {
        try {
            Check(Native.IsSupportedBrowser("chrome.exe"),"Chrome recognition failed");
            Check(Native.IsSupportedBrowser("msedge.exe"),"Edge recognition failed");
            Check(Native.IsSupportedBrowser("MSEdge.EXE"),"Browser name case handling failed");
            Check(!Native.IsSupportedBrowser("msedgewebview2.exe"),"WebView2 must not be treated as Edge");
            Check(!Native.IsSupportedBrowser("fakechrome.exe"),"Non-browser process accepted");
            foreach(double ratio in new[]{16.0/9,9.0/16,1,4.0/3,2.39,.1,10}) {
                foreach(int length in new[]{0,64,96,120,240,720,2400,9000}) {
                    Size size=Geometry.SizeFor(length,ratio);
                    Check(size.Width>=24 && size.Height>=24,"Minimum size failed");
                    Check(Math.Max(size.Width,size.Height)<=2400,"Maximum size failed");
                    Check(Math.Abs(size.Width-size.Height*ratio)<=Math.Max(1,ratio),"Aspect ratio failed");
                    for(int edge=1;edge<=8;edge++) {
                        var input=new Native.RECT(100,200,size.Width,size.Height);
                        var rect=Geometry.AspectRect(input,edge,ratio);
                        Check(Math.Abs(rect.Width-rect.Height*ratio)<=Math.Max(1,ratio),"Drag ratio failed");
                        if(edge==1 || edge==4 || edge==7) Check(rect.Right==input.Right,"Left drag anchor failed");
                        else Check(rect.Left==input.Left,"Right drag anchor failed");
                        if(edge==3 || edge==4 || edge==5) Check(rect.Bottom==input.Bottom,"Top drag anchor failed");
                        else Check(rect.Top==input.Top,"Bottom drag anchor failed");
                    }
                }
                var crop=Geometry.FitSource(640,480,ratio);
                Check(crop.Left>=0 && crop.Top>=0 && crop.Right<=640 && crop.Bottom<=480,"Crop out of bounds");
            }
            Check(Geometry.SizeFor(64,16.0/9)==new Size(64,36),"64px video minimum");
            Check(Geometry.SafeRatio(Double.NaN,1)==16.0/9,"NaN ratio");
            var stream=new MemoryStream(); Protocol.Write(stream,new {type="prepare",id="中文"});
            var parsed=Protocol.Read(new Fragmented(stream.ToArray()));
            Check(Protocol.Text(parsed,"id")=="中文","UTF8/fragmented message failed");
            Check(Protocol.Read(new MemoryStream())==null,"Clean EOF failed");
            Check(Rejects(new byte[]{1,0}),"Truncated header accepted");
            Check(Rejects(new byte[]{0,0,0,0}),"Empty message accepted");
            Check(Rejects(BitConverter.GetBytes(Protocol.MaxLength+1)),"Oversized message accepted");
            Check(Rejects(new byte[]{3,0,0,0,123}),"Truncated body accepted");
            Check(Rejects(new byte[]{2,0,0,0,91,93}),"Non-object JSON accepted");
            File.WriteAllText(path,"PASS: "+assertions+" assertions. Geometry, resize anchors, crop, UTF-8 framing, partial reads, malformed input.\r\n",Encoding.UTF8); return 0;
        } catch(Exception error) { File.WriteAllText(path,"FAIL: "+error,Encoding.UTF8); return 1; }
    }
}
}
