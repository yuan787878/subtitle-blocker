using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using SubtitleBlur;
class Backdrop:Form {
 protected override void OnPaint(PaintEventArgs e) {
  e.Graphics.Clear(Color.FromArgb(44,95,125));
  for(int x=0;x<Width;x+=12)using(var b=new SolidBrush(x%24==0?Color.FromArgb(54,112,138):Color.FromArgb(42,85,112)))e.Graphics.FillRectangle(b,x,0,12,Height);
  using(var f=new Font("Microsoft YaHei UI",28,FontStyle.Bold))e.Graphics.DrawString("这是一行测试字幕  Gaussian Blur",f,Brushes.White,65,220);
 }
}
class Tests {
 static void Assert(bool ok,string msg){if(!ok)throw new Exception(msg);}
 static void Wait(int ms){var until=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}}
 static Bitmap Capture(Rectangle r){var b=new Bitmap(r.Width,r.Height,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(b))g.CopyFromScreen(r.Location,Point.Empty,r.Size);return b;}
 static double Difference(Bitmap a,Bitmap b){double sum=0;for(int y=0;y<a.Height;y+=2)for(int x=0;x<a.Width;x+=2){var p=a.GetPixel(x,y);var q=b.GetPixel(x,y);sum+=Math.Abs(p.R-q.R)+Math.Abs(p.G-q.G)+Math.Abs(p.B-q.B);}return sum/(Math.Ceiling(a.Width/2.0)*Math.Ceiling(a.Height/2.0)*3);}
 static double Variation(Bitmap b){double sum=0;for(int y=0;y<b.Height;y+=2)for(int x=2;x<b.Width;x+=2){var p=b.GetPixel(x,y);var q=b.GetPixel(x-2,y);sum+=Math.Abs(p.R-q.R)+Math.Abs(p.G-q.G)+Math.Abs(p.B-q.B);}return sum;}
 [STAThread] static int Main(){string root=AppDomain.CurrentDomain.BaseDirectory;string dir=Path.Combine(root,"work");Directory.CreateDirectory(dir);try {
  Native.Dpi();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var uniform=new Bitmap(64,64,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(uniform))g.Clear(Color.FromArgb(80,120,160));using(var b=Gaussian.Apply(uniform,4,0))Assert(Difference(uniform,b)<.1,"Uniform color was changed");using(var b=Gaussian.Apply(uniform,4,100))Assert(b.GetPixel(30,30).R==0&&b.GetPixel(30,30).G==0&&b.GetPixel(30,30).B==0,"Full dim isn't black");}
  using(var synthetic=new Bitmap(800,110,PixelFormat.Format32bppArgb)){
   using(var g=Graphics.FromImage(synthetic)){g.Clear(Color.FromArgb(45,85,112));using(var f=new Font("Microsoft YaHei UI",28,FontStyle.Bold))g.DrawString("这是一行测试字幕 Gaussian Blur",f,Brushes.White,12,30);}
   using(var blurred=Gaussian.Apply(synthetic,18,8)){Assert(Variation(blurred)<Variation(synthetic)*.55,"Gaussian text-detail reduction failed");synthetic.Save(Path.Combine(dir,"original-subtitle.png"));blurred.Save(Path.Combine(dir,"blurred-result.png"));}
  }
  bool live=true;
  var a=Screen.PrimaryScreen.WorkingArea;
  try { using(var backdrop=new Backdrop())using(var overlay=new BlurOverlay()){
   backdrop.FormBorderStyle=FormBorderStyle.None;backdrop.StartPosition=FormStartPosition.Manual;backdrop.Bounds=new Rectangle(a.Left+30,a.Top+30,Math.Min(1100,a.Width-60),Math.Min(650,a.Height-60));backdrop.TopMost=true;backdrop.Show();Wait(400);
   var r=new Rectangle(backdrop.Left+60,backdrop.Top+205,Math.Min(780,backdrop.Width-120),100);
   using(var before=Capture(r)){
    overlay.Bounds=r;overlay.Strength=18;overlay.Dim=0;overlay.Show();overlay.SetLocked(true);Wait(1800);
    Assert(overlay.CaptureExcluded,"Windows refused capture exclusion");Assert(overlay.FrameCount>=3,"No live blur frames: "+overlay.LastError);
    using(var during=Capture(r)){
     double diff=Difference(before,during);Assert(diff<1,"Self-capture detected, difference="+diff);Console.WriteLine("Capture exclusion difference="+diff);
    }
    using(var visual=new Bitmap(r.Width,r.Height)){
     overlay.DrawToBitmap(visual,new Rectangle(Point.Empty,r.Size));visual.Save(Path.Combine(dir,"blurred-result.png"));before.Save(Path.Combine(dir,"original-subtitle.png"));
     Assert(Variation(visual)<Variation(before)*.55,"Blur didn't reduce text detail");Console.WriteLine("Blur detail ratio="+(Variation(visual)/Variation(before)));
    }
    int style=Native.GetWindowLong(overlay.Handle,-20);Assert((style&0x20)!=0,"Click-through flag missing");overlay.SetLocked(false);Assert((Native.GetWindowLong(overlay.Handle,-20)&0x20)==0,"Unlock didn't clear click-through");
    overlay.Size=new Size(550,70);overlay.Location=new Point(r.X+30,r.Y+20);Wait(600);Assert(overlay.FrameCount>4,"Resize interrupted capture");
    Console.WriteLine("Frames="+overlay.FrameCount);
   }
   overlay.Close();backdrop.Close();
  }} catch(System.ComponentModel.Win32Exception ex) {if(ex.NativeErrorCode!=6)throw;live=false;Console.WriteLine("SKIP live desktop capture: execution environment has no desktop DC.");}
  using(var panel=new ControlPanel()){
   panel.PersistSettings=false;panel.Show();Wait(700);using(var image=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(image,new Rectangle(Point.Empty,panel.Size));image.Save(Path.Combine(dir,"settings-preview.png"));}
   panel.ToggleVisible();Assert(!panel.Overlay.Visible,"Hide failed");panel.ToggleVisible();Assert(panel.Overlay.Visible,"Show failed");Assert(panel.HotkeyWarning=="","Global shortcuts unavailable: "+panel.HotkeyWarning);panel.Close();
  }
  File.WriteAllText(Path.Combine(dir,"test-report.txt"),"PASS: Gaussian uniform-color preservation, full darkening, synthetic subtitle detail reduction, UI rendering, show/hide, global hotkey registration.\r\nLive desktop test: "+(live?"PASS":"SKIPPED: desktop capture unavailable in execution environment")+"\r\n");Console.WriteLine("AVAILABLE TESTS PASSED; live desktop="+live);return 0;
 }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"test-report.txt"),ex.ToString());Console.WriteLine(ex);return 1;}}
}
