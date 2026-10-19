using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;

namespace CodeWhip {
  static partial class Native {
    [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFO {
      public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;
      public int XPels,YPels;public uint Used,Important;
    }
    [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc,ref BITMAPINFO info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint period);
    public delegate void TimerCallback(uint id,uint message,IntPtr user,IntPtr a,IntPtr b);
    [DllImport("winmm.dll")] public static extern uint timeSetEvent(uint delay,uint resolution,TimerCallback callback,IntPtr user,uint mode);
    [DllImport("winmm.dll")] public static extern uint timeKillEvent(uint id);
  }
  // One top-down premultiplied DIB per window. No bitmap allocation or HBITMAP copy per frame.
  sealed class LayerSurface:IDisposable {
    IntPtr dc,bitmap,original;public Bitmap Image;public Graphics Graphics;
    public int Width,Height;
    public LayerSurface(int w,int h){
      Width=w;Height=h;IntPtr screen=Native.GetDC(IntPtr.Zero);
      try{
        dc=Native.CreateCompatibleDC(screen);
        var info=new Native.BITMAPINFO{Size=40,Width=w,Height=-h,Planes=1,Bits=32};IntPtr bits;
        bitmap=Native.CreateDIBSection(screen,ref info,0,out bits,IntPtr.Zero,0);
        if(bitmap==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        original=Native.SelectObject(dc,bitmap);
        Image=new Bitmap(w,h,w*4,PixelFormat.Format32bppPArgb,bits);Graphics=Graphics.FromImage(Image);
        Graphics.SmoothingMode=SmoothingMode.AntiAlias;Graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
        Graphics.CompositingQuality=CompositingQuality.HighSpeed;
      }finally{Native.ReleaseDC(IntPtr.Zero,screen);}
    }
    public void Present(IntPtr window,Point location,byte opacity){
      var at=new Native.POINT(location.X,location.Y);var zero=new Native.POINT(0,0);var size=new Native.SIZE(Width,Height);
      var blend=new Native.BLEND{Op=0,Flags=0,Alpha=opacity,Format=1};
      if(!Native.UpdateLayeredWindow(window,IntPtr.Zero,ref at,ref size,dc,ref zero,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Dispose(){
      if(Graphics!=null)Graphics.Dispose();if(Image!=null)Image.Dispose();
      if(original!=IntPtr.Zero)Native.SelectObject(dc,original);if(bitmap!=IntPtr.Zero)Native.DeleteObject(bitmap);if(dc!=IntPtr.Zero)Native.DeleteDC(dc);
      Graphics=null;Image=null;dc=bitmap=original=IntPtr.Zero;
    }
  }
  class LayerForm:Form {
    LayerSurface surface;protected byte CanvasOpacity=255;
    protected void RenderCanvas(Action<Graphics> draw){
      if(Width<1||Height<1||IsDisposed)return;
      if(surface==null||surface.Width!=Width||surface.Height!=Height){if(surface!=null)surface.Dispose();surface=new LayerSurface(Width,Height);}
      surface.Graphics.Clear(Color.Transparent);draw(surface.Graphics);surface.Present(Handle,Location,CanvasOpacity);
    }
    protected override void OnPaintBackground(PaintEventArgs e){}
    protected override void Dispose(bool disposing){if(disposing&&surface!=null){surface.Dispose();surface=null;}base.Dispose(disposing);}
  }
  sealed class WhipModel {
    public string Material="leather";
    public PointF[] Nodes=new PointF[37];PointF[] previous=new PointF[37];float[] lengths=new float[36];
    public List<PointF[]> Trails=new List<PointF[]>();public double Time;bool seeded;float aim=-1.7f,aimVelocity;
    const double Step=1.0/120;
    static PointF Mix(PointF a,PointF b,float t){return new PointF(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);}
    static PointF Bezier(PointF a,PointF b,PointF c,PointF d,float t){float u=1-t;return new PointF(u*u*u*a.X+3*u*u*t*b.X+3*u*t*t*c.X+t*t*t*d.X,u*u*u*a.Y+3*u*u*t*b.Y+3*u*t*t*c.Y+t*t*t*d.Y);}
    static float Smooth(double t){float f=(float)Math.Max(0,Math.Min(1,t));return f*f*(3-2*f);}
    public void Reset(){seeded=false;Time=0;Trails.Clear();aim=-1.7f;aimVelocity=0;}
    public void Advance(double time,int width,int height){
      int iterations=0;
      if(!seeded)Simulate(0,width,height);
      while(Time<time&&iterations++<32){Time+=Step;Simulate(Time,width,height);}
      if(Time<time)Time=time;
      Trails.Add((PointF[])Nodes.Clone());while(Trails.Count>5)Trails.RemoveAt(0);
    }
    void Simulate(double t,int w,int h){
      bool first=!seeded;
      float wind=Smooth(t/.17),swing=Smooth((t-.17)/.19),back=Smooth((t-.43)/.52);
      PointF grip=new PointF(w*(.15f-.06f*wind+.21f*swing-.2f*back),h*(.75f-.22f*wind+.07f*swing+.16f*back));
      var target=new PointF(w*.76f,h*.45f);
      float desiredAngle=-1.9f+.5f*wind+1.35f*swing-1.5f*back;
      aimVelocity=(aimVelocity+(desiredAngle-aim)*.15f)*.7f;aim+=aimVelocity;
      float total=0;for(int i=0;i<lengths.Length;i++){lengths[i]=w*.0205f*(1-i/(float)lengths.Length*.40f);total+=lengths[i];}
      var end=Mix(new PointF(grip.X+w*.28f,grip.Y-h*.20f),target,swing);
      end=Mix(end,new PointF(grip.X+w*.22f,grip.Y+h*.2f),back);
      var c1=new PointF(grip.X+(float)Math.Cos(aim)*total*.58f,grip.Y+(float)Math.Sin(aim)*total*.58f);
      var c2=new PointF(end.X-w*.12f*(1-swing)+w*.07f*back,end.Y-h*.50f*(1-swing)+h*.05f*back);
      if(!seeded){
        for(int i=0;i<Nodes.Length;i++)Nodes[i]=previous[i]=Bezier(grip,c1,c2,end,i/(float)(Nodes.Length-1));seeded=true;
      }
      for(int i=1;i<Nodes.Length;i++){
        var old=Nodes[i];float u=i/(float)(Nodes.Length-1),damping=.976f-u*.004f;
        var p=new PointF(old.X+(old.X-previous[i].X)*damping,old.Y+(old.Y-previous[i].Y)*damping+h*.00014f);
        {
          var guide=Bezier(grip,c1,c2,end,u);float wave=(float)Math.Sin(u*6.5-t*17)*u*(1-u)*(1-swing)*w*.09f;guide.Y+=wave;
          p=Mix(p,guide,.022f+(1-u)*.011f);
        }
        Nodes[i]=p;previous[i]=old;
      }
      Nodes[0]=previous[0]=grip;
      bool pin=t>=.34&&t<.43;
      for(int k=0;k<22;k++){
        Nodes[0]=grip;
        var dir=new PointF((float)Math.Cos(aim),(float)Math.Sin(aim));
        Nodes[1]=Mix(Nodes[1],new PointF(grip.X+dir.X*lengths[0],grip.Y+dir.Y*lengths[0]),.88f);
        Nodes[2]=Mix(Nodes[2],new PointF(grip.X+dir.X*(lengths[0]+lengths[1]),grip.Y+dir.Y*(lengths[0]+lengths[1])),.35f);
        if(pin)Nodes[Nodes.Length-1]=target;
        for(int i=0;i<lengths.Length;i++){
          float dx=Nodes[i+1].X-Nodes[i].X,dy=Nodes[i+1].Y-Nodes[i].Y,dist=(float)Math.Sqrt(dx*dx+dy*dy);if(dist<.0001f)continue;
          float correction=(dist-lengths[i])/dist;
          if(i==0)Nodes[i+1]=new PointF(Nodes[i+1].X-dx*correction,Nodes[i+1].Y-dy*correction);
          else if(pin&&i==lengths.Length-1)Nodes[i]=new PointF(Nodes[i].X+dx*correction,Nodes[i].Y+dy*correction);
          else {Nodes[i]=new PointF(Nodes[i].X+dx*correction*.5f,Nodes[i].Y+dy*correction*.5f);Nodes[i+1]=new PointF(Nodes[i+1].X-dx*correction*.5f,Nodes[i+1].Y-dy*correction*.5f);}
        }
        // Stiff base, supple tip. Smooth excess turns without making the whole rope rigid.
        for(int i=2;i<Nodes.Length-1;i++){
          float ax=Nodes[i].X-Nodes[i-1].X,ay=Nodes[i].Y-Nodes[i-1].Y,bx=Nodes[i+1].X-Nodes[i].X,by=Nodes[i+1].Y-Nodes[i].Y;
          float angle=(float)Math.Atan2(ax*by-ay*bx,ax*bx+ay*by),u=i/(float)Nodes.Length,max=.45f+u*1.85f;
          if(Math.Abs(angle)>max){float heading=(float)Math.Atan2(ay,ax)+Math.Sign(angle)*max;
            var p=new PointF(Nodes[i].X+(float)Math.Cos(heading)*lengths[i],Nodes[i].Y+(float)Math.Sin(heading)*lengths[i]);
            if(!(pin&&i==Nodes.Length-2))Nodes[i+1]=Mix(Nodes[i+1],p,.22f*(1-u)+.06f);
          }
        }
      }
      if(pin)Nodes[Nodes.Length-1]=target;
      if(first)previous=(PointF[])Nodes.Clone();
    }
    static PointF At(PointF[] p,int i){if(i<0)return new PointF(2*p[0].X-p[1].X,2*p[0].Y-p[1].Y);if(i>=p.Length)return new PointF(2*p[p.Length-1].X-p[p.Length-2].X,2*p[p.Length-1].Y-p[p.Length-2].Y);return p[i];}
    static void Curve(PointF[] pts,int i,out PointF a,out PointF b,out PointF c,out PointF d){
      var p0=At(pts,i-1);a=At(pts,i);d=At(pts,i+1);var p3=At(pts,i+2);
      b=new PointF(a.X+(d.X-p0.X)/6,a.Y+(d.Y-p0.Y)/6);c=new PointF(d.X-(p3.X-a.X)/6,d.Y-(p3.Y-a.Y)/6);
    }
    public void Draw(Graphics g,float scale,int alpha,bool trails){
      if(trails)for(int k=0;k<Trails.Count-1;k++)DrawRope(g,Trails[k],scale,(int)(alpha*.055*(k+1)),false);
      DrawRope(g,Nodes,scale,alpha,true);DrawHandle(g,Nodes[0],Nodes[1],scale,alpha);
    }
    void DrawRope(Graphics g,PointF[] pts,float s,int alpha,bool texture){
      alpha=Math.Max(0,Math.Min(255,alpha));
      bool braided=Material=="braided",rubber=Material=="rubber";
      Color baseColor=rubber?Color.FromArgb(41,45,51):braided?Color.FromArgb(176,137,76):Color.FromArgb(143,86,42);
      Color shine=rubber?Color.FromArgb(116,126,138):braided?Color.FromArgb(246,216,145):Color.FromArgb(237,181,103);
      if(!texture)using(var outline=new GraphicsPath()){
        for(int i=0;i<pts.Length-1;i++){PointF a,b,c,d;Curve(pts,i,out a,out b,out c,out d);outline.AddBezier(a,b,c,d);}
        using(var pen=new Pen(Color.FromArgb(alpha,shine),3*s)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawPath(pen,outline);}return;
      }
      for(int layer=0;layer<3;layer++)for(int i=0;i<pts.Length-1;i++){
        PointF a,b,c,d;Curve(pts,i,out a,out b,out c,out d);float u=i/(float)(pts.Length-1),width=(1.1f+9.5f*(float)Math.Pow(1-u,1.1))*s;
        var color=layer==0?Color.FromArgb((int)(alpha*.36),shine):layer==1?Color.FromArgb(alpha,22,24,27):Color.FromArgb(alpha,baseColor);
        using(var pen=new Pen(color,width+(layer==0?4:layer==1?2:0)*s)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawBezier(pen,a,b,c,d);}
        if(layer==2&&texture){
          float dx=d.X-a.X,dy=d.Y-a.Y,len=(float)Math.Sqrt(dx*dx+dy*dy)+.001f,nx=-dy/len,ny=dx/len;
          using(var pen=new Pen(Color.FromArgb((int)(alpha*.74),shine),Math.Max(.6f,1.25f*(1-u)*s)))g.DrawBezier(pen,new PointF(a.X+nx*width*.22f,a.Y+ny*width*.22f),new PointF(b.X+nx*width*.22f,b.Y+ny*width*.22f),new PointF(c.X+nx*width*.22f,c.Y+ny*width*.22f),new PointF(d.X+nx*width*.22f,d.Y+ny*width*.22f));
          if(braided){
            for(int j=0;j<3;j++){var mid=Bezier(a,b,c,d,(j+.5f)/3);float direction=j%2==0?1:-1;using(var pen=new Pen(Color.FromArgb((int)(alpha*.9),102,74,36),Math.Max(.65f,s)))g.DrawLine(pen,mid.X-nx*width*.4f-dx*.035f*direction,mid.Y-ny*width*.4f-dy*.035f*direction,mid.X+nx*width*.4f+dx*.035f*direction,mid.Y+ny*width*.4f+dy*.035f*direction);}
          }else if(!rubber&&i%2==0){var mid=Bezier(a,b,c,d,.5f);using(var pen=new Pen(Color.FromArgb((int)(alpha*.8),66,37,21),.9f*s))g.DrawLine(pen,mid.X-nx*width*.35f-dx*.06f,mid.Y-ny*width*.35f-dy*.06f,mid.X+nx*width*.35f+dx*.06f,mid.Y+ny*width*.35f+dy*.06f);}
        }
      }
    }
    void DrawHandle(Graphics g,PointF start,PointF next,float s,int alpha){
      var state=g.Save();g.TranslateTransform(start.X,start.Y);g.RotateTransform((float)(Math.Atan2(next.Y-start.Y,next.X-start.X)*180/Math.PI));
      var rect=new RectangleF(-104*s,-11*s,104*s,22*s);
      bool rubber=Material=="rubber";
      using(var brush=new LinearGradientBrush(rect,rubber?Color.FromArgb(alpha,74,80,89):Color.FromArgb(alpha,202,141,75),rubber?Color.FromArgb(alpha,21,25,29):Color.FromArgb(alpha,56,31,21),90))g.FillRectangle(brush,rect);
      using(var pen=new Pen(Color.FromArgb(alpha,255,238,202),2*s))g.DrawRectangle(pen,rect.X-1*s,rect.Y-1*s,rect.Width+2*s,rect.Height+2*s);
      using(var pen=new Pen(Color.FromArgb(alpha,34,24,20),2*s))g.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);
      for(int i=0;i<12;i++)using(var pen=new Pen(Color.FromArgb(alpha,65,34,22),1.5f*s))g.DrawLine(pen,(-102+i*8)*s,-10*s,(-90+i*8)*s,10*s);
      using(var b=new SolidBrush(Color.FromArgb(alpha,193,183,157))){g.FillRectangle(b,-110*s,-13*s,9*s,26*s);g.FillRectangle(b,-6*s,-13*s,8*s,26*s);}
      g.Restore(state);
    }
  }
}
