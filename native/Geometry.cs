using System;
using System.Drawing;

namespace MiniFloat {
internal static class Geometry {
    internal static double SafeRatio(double width, double height) {
        if (Double.IsNaN(width) || Double.IsNaN(height) || Double.IsInfinity(width) || Double.IsInfinity(height) || width<=0 || height<=0) return 16.0/9;
        return Math.Max(.1,Math.Min(10,width/height));
    }
    internal static Size SizeFor(double longSide, double ratio) {
        longSide=Math.Max(Math.Max(64,24*Math.Max(ratio,1/ratio)),Math.Min(2400,longSide));
        return ratio>=1 ? new Size((int)Math.Round(longSide),Math.Max(24,(int)Math.Round(longSide/ratio))) : new Size(Math.Max(24,(int)Math.Round(longSide*ratio)),(int)Math.Round(longSide));
    }
    internal static Native.RECT AspectRect(Native.RECT rect, int edge, double ratio) {
        // Left/right and corner drags follow width; top/bottom follow height.
        double longer=(edge==3 || edge==6) ? rect.Height*Math.Max(1,ratio) : rect.Width*Math.Max(1,1/ratio);
        Size size=SizeFor(longer,ratio);
        bool left=edge==1 || edge==4 || edge==7;
        bool top=edge==3 || edge==4 || edge==5;
        return new Native.RECT(left?rect.Right-size.Width:rect.Left,top?rect.Bottom-size.Height:rect.Top,size.Width,size.Height);
    }
    internal static Native.RECT FitSource(int width,int height,double ratio) {
        // PiP can have small client-area margins: crop only letterboxing, never stretch.
        int w=width,h=(int)Math.Round(width/ratio);
        if (h>height) { h=height; w=(int)Math.Round(height*ratio); }
        return new Native.RECT((width-w)/2,(height-h)/2,Math.Max(1,w),Math.Max(1,h));
    }
}
}
