using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Media;
using System.Threading;

namespace CodeWhip {
  static class Program {
    [STAThread] static void Main(string[] args) {
      Console.InputEncoding=Encoding.UTF8; Console.OutputEncoding=new UTF8Encoding(false);
      try { Native.SetProcessDpiAwareness(2); } catch { try{Native.SetProcessDPIAware();}catch{} }
      Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
      if(args.Length==2 && args[0]=="--render-materials") {Overlay.ExportMaterials(args[1]);return;}
      var form=new Overlay();
      var handle=form.Handle;
      var reader=new Thread(()=>{
        try{string line;while((line=Console.ReadLine())!=null){string item=line;form.BeginInvoke(new Action(()=>form.Receive(item)));}}
        catch(Exception e){Console.Error.WriteLine(e.Message);}
        try{form.BeginInvoke(new Action(()=>form.Close()));}catch{}
      });reader.IsBackground=true;reader.Start();
      Application.Run(form);
    }
  }
  static partial class Native {
    [StructLayout(LayoutKind.Sequential)] public struct POINT{public int X,Y;public POINT(int x,int y){X=x;Y=y;}}
    [StructLayout(LayoutKind.Sequential)] public struct SIZE{public int W,H;public SIZE(int w,int h){W=w;H=h;}}
    [StructLayout(LayoutKind.Sequential,Pack=1)] public struct BLEND{public byte Op,Flags,Alpha,Format;}
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h,IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dc,ref POINT at,ref SIZE size,IntPtr src,ref POINT zero,int key,ref BLEND blend,int flags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int value);
    [DllImport("dwmapi.dll")] public static extern int DwmFlush();
  }
  static class Read {
    public static string S(Dictionary<string,object>d,string k){object v;return d.TryGetValue(k,out v)&&v!=null?Convert.ToString(v):"";}
    public static double N(Dictionary<string,object>d,string k,double fallback){object v;return d.TryGetValue(k,out v)?Convert.ToDouble(v):fallback;}
    public static bool B(Dictionary<string,object>d,string k,bool fallback){object v;return d.TryGetValue(k,out v)?Convert.ToBoolean(v):fallback;}
    public static Dictionary<string,object>D(Dictionary<string,object>d,string k){object v;return d.TryGetValue(k,out v)&&v is Dictionary<string,object>?(Dictionary<string,object>)v:new Dictionary<string,object>();}
  }

  sealed class Overlay:LayerForm {
    const double Impact=.20,Duration=.64,ShakeDuration=.12;
    JavaScriptSerializer json=new JavaScriptSerializer();
    System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();Stopwatch clock=new Stopwatch();
    WhipModel whip=new WhipModel();
    Dictionary<string,object> payload=new Dictionary<string,object>(),options=new Dictionary<string,object>();
    Bitmap snapshot;volatile bool active;bool hit,precision,captureDisabled;
    Native.TimerCallback framePump;uint pumpId;int frameQueued;
    double impactAt=Impact,renderMillis,nextFrame;int frameCount;
    PointF impactPoint;SoundPlayer sound;MemoryStream audio;
    public Overlay(){
      FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
      Bounds=Screen.PrimaryScreen.Bounds;
      timer.Interval=50;timer.Tick+=(s,e)=>Tick();timer.Start();
      framePump=(id,message,user,a,b)=>{
        if(!active||!IsHandleCreated||IsDisposed||Interlocked.Exchange(ref frameQueued,1)!=0)return;
        try{BeginInvoke(new Action(()=>{try{Tick();}finally{Interlocked.Exchange(ref frameQueued,0);}}));}
        catch{Interlocked.Exchange(ref frameQueued,0);}
      };
    }
    protected override bool ShowWithoutActivation{get{return true;}}
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x00080000|0x00000080;p.ExStyle|=0x20;return p;}}
    protected override void SetVisibleCore(bool value){base.SetVisibleCore(active&&value);}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Emit("ready","","");}
    void Precision(bool enable){if(enable==precision)return;precision=enable;if(enable){Native.timeBeginPeriod(1);pumpId=Native.timeSetEvent(4,1,framePump,IntPtr.Zero,0x101);}else{if(pumpId!=0)Native.timeKillEvent(pumpId);pumpId=0;Native.timeEndPeriod(1);}}
    public void Receive(string line){
      try{
        var data=json.Deserialize<Dictionary<string,object>>(line);string kind=Read.S(data,"kind");
        if(kind=="quit"){Close();return;}if(kind=="stop"){Stop();return;}
        if(kind!="strike")return;
        Stop();payload=data;options=Read.D(data,"settings");Bounds=Screen.FromHandle(Native.GetForegroundWindow()).Bounds;
        impactPoint=new PointF(Width*.708f,Height*.46f);impactAt=Impact;hit=false;whip.Reset();clock.Restart();frameCount=0;renderMillis=0;
        Emit("strikeStarted",Read.S(payload,"id"),"material="+Read.S(options,"material"));
        if(Read.B(options,"sound",true))PlaySound(Read.N(options,"volume",.25),false);
        if(Read.B(options,"reducedMotion",false))return;
        active=true;whip.Material=Read.S(options,"material");Precision(true);nextFrame=1.0/60;Show();Frame(.001);
      }catch(Exception ex){Stop();Emit("error","",ex.Message);}
    }
    void Stop(){
      active=false;timer.Interval=50;Hide();Precision(false);clock.Reset();hit=false;whip.Reset();
      if(snapshot!=null){snapshot.Dispose();snapshot=null;}if(sound!=null){sound.Stop();sound.Dispose();sound=null;}if(audio!=null){audio.Dispose();audio=null;}
    }
    void CaptureDesktop(){
      if(snapshot!=null){snapshot.Dispose();snapshot=null;}
      if(Read.S(options,"intensity")=="off"||captureDisabled)return;
      try{Hide();Native.DwmFlush();snapshot=new Bitmap(Width,Height,PixelFormat.Format24bppRgb);using(var g=Graphics.FromImage(snapshot))g.CopyFromScreen(Left,Top,0,0,Size,CopyPixelOperation.SourceCopy);Show();}
      catch(Exception ex){if(snapshot!=null){snapshot.Dispose();snapshot=null;}Show();Emit("error","","Desktop capture: "+ex.Message);}
    }
    void Tick(){
      if(!active)return;double elapsed=clock.Elapsed.TotalSeconds;
      if(elapsed<nextFrame)return;nextFrame=elapsed+.014;
      try{
        if(elapsed>=Impact&&!hit){hit=true;CaptureDesktop();Emit("impact",Read.S(payload,"id"),"captured="+(snapshot!=null));}
        if(elapsed>Duration){active=false;Hide();Precision(false);Emit("animationStats","","frames="+frameCount+";duration="+Duration+";material="+whip.Material);return;}
        Frame(elapsed);
        if(snapshot!=null&&elapsed-impactAt>ShakeDuration){snapshot.Dispose();snapshot=null;}
      }catch(Exception ex){Stop();Emit("error","",ex.Message);}
    }
    void Frame(double t){var watch=Stopwatch.StartNew();RenderCanvas(g=>PaintScene(g,t));renderMillis+=watch.Elapsed.TotalMilliseconds;frameCount++;}
    public void PaintScene(Graphics g,double t){
      g.SmoothingMode=SmoothingMode.AntiAlias;float s=Width/1440f;double since=t-impactAt;
      if(snapshot!=null&&since>=0&&since<ShakeDuration){
        double power=Math.Pow(1-since/ShakeDuration,2),strength=Read.S(options,"intensity")=="strong"?23:Read.S(options,"intensity")=="light"?6:13;
        int dx=(int)(Math.Cos(since*100)*strength*power*s),dy=(int)(Math.Sin(since*79)*strength*.42*power*s);
        using(var b=new SolidBrush(Color.FromArgb(22,20,24)))g.FillRectangle(b,ClientRectangle);g.DrawImageUnscaled(snapshot,dx,dy);
      }
      if(since>=0&&since<.055&&Read.B(options,"flash",true))using(var b=new SolidBrush(Color.FromArgb((int)(30*(1-since/.055)),255,232,159)))g.FillRectangle(b,ClientRectangle);
      whip.Advance(t*1.8,(int)(Width*.8),(int)(Height*.8));
      int alpha=t>.44?(int)(255*Math.Max(0,1-(t-.44)/.18)):255;
      if(alpha>0){var state=g.Save();g.TranslateTransform(Width*.1f,Height*.1f);whip.Draw(g,s*.82f,alpha,Read.B(options,"trails",true));g.Restore(state);}
      if(since>=0&&since<.22)DrawImpact(g,impactPoint,(float)since,s,Read.B(options,"flash",true));
    }
    static void DrawImpact(Graphics g,PointF at,float since,float s,bool flash){
      float progress=since/.22f;int alpha=(int)(195*(1-progress));
      for(int i=0;i<18;i++){
        double angle=i*2.399963;float radius=(float)((28+i%5*14)*Math.Sqrt(progress))*s;
        float x=at.X+(float)Math.Cos(angle)*radius,y=at.Y+(float)Math.Sin(angle)*radius+progress*progress*50*s;
        using(var b=new SolidBrush(Color.FromArgb(alpha,210,188,144)))g.FillEllipse(b,x,y,(2+i%3)*s,(1+i%3)*s);
      }
      if(flash&&since<.12){
        int opacity=(int)(160*(1-since/.12));
        for(int i=0;i<3;i++)using(var pen=new Pen(Color.FromArgb(opacity,161,48,36),(2-i*.4f)*s))
          g.DrawLine(pen,at.X+(-16+i*13)*s,at.Y-21*s,at.X+(-4+i*13)*s,at.Y+18*s);
      }
    }
    void PlaySound(double volume,bool immediate){
      if(sound!=null){sound.Stop();sound.Dispose();sound=null;}if(audio!=null){audio.Dispose();audio=null;}
      int rate=22050,len=(int)(rate*(immediate?.35:.55));double impact=immediate?0:Impact;var samples=new short[len];var rng=new Random();
      for(int i=0;i<len;i++){
        double t=i/(double)rate,noise=rng.NextDouble()*2-1;
        double wind=t<impact?Math.Sin(Math.PI*t/impact)*noise*.16:0;
        double crack=t>=impact?noise*Math.Exp(-(t-impact)*48)*.85+Math.Sin(2*Math.PI*82*(t-impact))*Math.Exp(-(t-impact)*27)*.28:0;
        samples[i]=(short)(Math.Max(-1,Math.Min(1,wind+crack))*27000*Math.Max(0,Math.Min(1,volume)));
      }
      audio=new MemoryStream();var writer=new BinaryWriter(audio);writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+len*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(len*2);foreach(short value in samples)writer.Write(value);writer.Flush();audio.Position=0;sound=new SoundPlayer(audio);sound.Play();
    }
    static void Emit(string action,string id,string message){Console.WriteLine(new JavaScriptSerializer().Serialize(new {action=action,id=id,message=message}));Console.Out.Flush();}
    protected override void Dispose(bool disposing){if(disposing){Stop();timer.Dispose();}base.Dispose(disposing);}
    public static void ExportMaterials(string folder){
      Directory.CreateDirectory(folder);string[] materials={"leather","braided","rubber"};
      using(var overlay=new Overlay()){
        overlay.timer.Stop();overlay.Bounds=new Rectangle(0,0,1440,900);overlay.captureDisabled=true;
        overlay.options=new Dictionary<string,object>{{"flash",false},{"trails",true},{"intensity","off"}};
        overlay.impactPoint=new PointF(1440*.708f,900*.46f);
        foreach(string material in materials){
          overlay.whip.Material=material;overlay.whip.Reset();
          for(int frame=0;frame<20;frame++){
            double t=frame/30.0;
            using(var bmp=new Bitmap(1440,900))using(var g=Graphics.FromImage(bmp)){
              g.Clear(Color.FromArgb(26,29,35));overlay.PaintScene(g,t);
              bmp.Save(Path.Combine(folder,material+"-"+frame.ToString("D2")+".png"),ImageFormat.Png);
            }
          }
        }
      }
    }
  }
}
