using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace SubtitleBlur {
public static class Native {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", SetLastError=true)] public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int index, int value);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint color, byte alpha, uint flags);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    public static void Dpi() { try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { SetProcessDPIAware(); } }
}

[Serializable]
public class Settings {
    public int X=200, Y=700, Width=900, Height=110, Blur=22, Dim=8;
    public bool Locked=false;
    public static string FilePath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.xml"); } }
    public static Settings Load() {
        Settings s=null;
        try { using(var f=File.OpenRead(FilePath)) s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(f); } catch { }
        if(s==null) { s=new Settings(); var a=Screen.PrimaryScreen.WorkingArea; s.Width=Math.Min(900,a.Width-40); s.X=a.Left+(a.Width-s.Width)/2; s.Y=a.Bottom-190; }
        s.Width=Math.Max(120,Math.Min(3840,s.Width)); s.Height=Math.Max(32,Math.Min(2160,s.Height));
        s.Blur=Math.Max(1,Math.Min(48,s.Blur)); s.Dim=Math.Max(0,Math.Min(100,s.Dim));
        bool onScreen=false; foreach(var screen in Screen.AllScreens) if(Rectangle.Intersect(screen.WorkingArea,new Rectangle(s.X,s.Y,s.Width,s.Height)).Width>=60 && Rectangle.Intersect(screen.WorkingArea,new Rectangle(s.X,s.Y,s.Width,s.Height)).Height>=24) onScreen=true;
        if(!onScreen) { var a=Screen.PrimaryScreen.WorkingArea; s.X=a.Left+40; s.Y=a.Bottom-Math.Min(s.Height+80,a.Height); }
        return s;
    }
    public void Save() {
        string temp=FilePath+".tmp";
        using(var f=File.Create(temp)) new XmlSerializer(typeof(Settings)).Serialize(f,this);
        if(File.Exists(FilePath)) File.Replace(temp,FilePath,null); else File.Move(temp,FilePath);
    }
}

public static class Gaussian {
    // Two separable passes apply an actual Gaussian kernel, not a translucent cover.
    public static Bitmap Apply(Bitmap source, double sigma, int dim) {
        int w=source.Width,h=source.Height,n=w*h*4;
        byte[] input=new byte[n],output=new byte[n]; float[] middle=new float[n];
        var rect=new Rectangle(0,0,w,h);
        var data=source.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try { for(int y=0;y<h;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),input,y*w*4,w*4); } finally { source.UnlockBits(data); }
        sigma=Math.Max(.35,sigma); int radius=Math.Min(80,(int)Math.Ceiling(sigma*3));
        double[] kernel=new double[radius*2+1]; double sum=0;
        for(int k=-radius;k<=radius;k++) { double v=Math.Exp(-(k*k)/(2*sigma*sigma)); kernel[k+radius]=v; sum+=v; }
        for(int k=0;k<kernel.Length;k++) kernel[k]/=sum;
        Parallel.For(0,h,y=> { for(int x=0;x<w;x++) { int dest=(y*w+x)*4; double b=0,g=0,r=0;
            for(int k=-radius;k<=radius;k++) { int px=Math.Max(0,Math.Min(w-1,x+k)),idx=(y*w+px)*4; double f=kernel[k+radius]; b+=input[idx]*f; g+=input[idx+1]*f; r+=input[idx+2]*f; }
            middle[dest]=(float)b;middle[dest+1]=(float)g;middle[dest+2]=(float)r;
        }});
        double shade=1-Math.Max(0,Math.Min(100,dim))/100.0;
        Parallel.For(0,h,y=> { for(int x=0;x<w;x++) { int dest=(y*w+x)*4; double b=0,g=0,r=0;
            for(int k=-radius;k<=radius;k++) { int py=Math.Max(0,Math.Min(h-1,y+k)),idx=(py*w+x)*4; double f=kernel[k+radius]; b+=middle[idx]*f;g+=middle[idx+1]*f;r+=middle[idx+2]*f; }
            output[dest]=(byte)Math.Min(255,Math.Round(b*shade));output[dest+1]=(byte)Math.Min(255,Math.Round(g*shade));output[dest+2]=(byte)Math.Min(255,Math.Round(r*shade));output[dest+3]=255;
        }});
        var result=new Bitmap(w,h,PixelFormat.Format32bppArgb); var dst=result.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try { for(int y=0;y<h;y++) Marshal.Copy(output,y*w*4,IntPtr.Add(dst.Scan0,y*dst.Stride),w*4); } finally { result.UnlockBits(dst); }
        return result;
    }
    public static Bitmap Capture(Rectangle bounds,int sigma,int dim) {
        int factor=Math.Max(2,Math.Max((int)Math.Ceiling(bounds.Width/480.0),(int)Math.Ceiling(bounds.Height/240.0)));
        int padding=3*sigma;
        var requested=Rectangle.Inflate(bounds,padding,padding); var captured=Rectangle.Intersect(requested,SystemInformation.VirtualScreen);
        if(captured.Width<=0||captured.Height<=0) throw new InvalidOperationException("遮挡框在屏幕之外");
        using(var full=new Bitmap(requested.Width,requested.Height,PixelFormat.Format32bppArgb)) {
            using(var g=Graphics.FromImage(full)) { g.Clear(Color.Black);g.CopyFromScreen(captured.Location,new Point(captured.X-requested.X,captured.Y-requested.Y),captured.Size,CopyPixelOperation.SourceCopy); }
            int w=Math.Max(1,(int)Math.Ceiling(full.Width/(double)factor)),h=Math.Max(1,(int)Math.Ceiling(full.Height/(double)factor));
            using(var small=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(small)) { g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(full,new Rectangle(0,0,w,h)); }
                using(var blurred=Apply(small,sigma/(double)factor,dim)) {
                    var frame=new Bitmap(Math.Max(1,bounds.Width/factor),Math.Max(1,bounds.Height/factor),PixelFormat.Format32bppArgb);
                    using(var g=Graphics.FromImage(frame)) { g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(blurred,new Rectangle(0,0,frame.Width,frame.Height),new RectangleF(padding*w/(float)full.Width,padding*h/(float)full.Height,bounds.Width*w/(float)full.Width,bounds.Height*h/(float)full.Height),GraphicsUnit.Pixel); }
                    return frame;
                }
            }
        }
    }
}

public class BlurOverlay : Form {
    public int Strength=22, Dim=8;
    public bool Locked { get; private set; }
    public bool CaptureExcluded { get; private set; }
    public string LastError { get; private set; }
    public int FrameCount { get; private set; }
    public event Action GeometryChanged;
    public event Action RequestSettings;
    public event Action RequestLock;
    private Bitmap frame;
    private bool rendering,closing;
    private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    public BlurOverlay() {
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
        BackColor=Color.FromArgb(32,38,40);MinimumSize=new Size(120,32);MaximumSize=new Size(3840,2160);
        SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        timer.Interval=50;timer.Tick+=(s,e)=>RefreshFrame();
        var menu=new ContextMenuStrip();menu.Items.Add("打开设置",null,(s,e)=>ShowSettings());menu.Items.Add("锁定并穿透鼠标",null,(s,e)=>{if(RequestLock!=null)RequestLock();});ContextMenuStrip=menu;
        DoubleClick+=(s,e)=>ShowSettings();Move+=(s,e)=>Changed();SizeChanged+=(s,e)=>Changed();
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var cp=base.CreateParams;cp.ExStyle|=0x80|0x80000;return cp; } }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);Native.SetLayeredWindowAttributes(Handle,0,255,2);
        CaptureExcluded=Native.SetWindowDisplayAffinity(Handle,0x11);
        if(!CaptureExcluded) LastError="系统未允许实时模糊，已使用纯色遮挡。";
    }
    protected override void OnShown(EventArgs e) { base.OnShown(e);timer.Start();RefreshFrame(); }
    public void SetLocked(bool value) {
        Locked=value;int style=Native.GetWindowLong(Handle,-20);style=value?style|0x20:style&~0x20;Native.SetWindowLong(Handle,-20,style);Invalidate();
    }
    private void Changed() { if(GeometryChanged!=null)GeometryChanged();Invalidate(); }
    private void ShowSettings() { if(RequestSettings!=null)RequestSettings(); }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);if(!Locked&&e.Button==MouseButtons.Left) { Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero); }
    }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x84&&!Locked) {
            long l=m.LParam.ToInt64();var p=PointToClient(new Point((short)(l&0xffff),(short)((l>>16)&0xffff)));
            int edge=8;bool left=p.X<edge,right=p.X>=Width-edge,top=p.Y<edge,bottom=p.Y>=Height-edge;
            int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1;
            m.Result=new IntPtr(hit);return;
        }
        base.WndProc(ref m);
    }
    public void RefreshFrame() {
        if(closing||rendering||!Visible||WindowState==FormWindowState.Minimized||!CaptureExcluded)return;
        Rectangle snapshot=Bounds;int blur=Strength,dim=Dim;rendering=true;
        Task.Factory.StartNew(()=> {
            Bitmap next=null;string error=null;
            try { next=Gaussian.Capture(snapshot,blur,dim); } catch(Exception ex) { error=ex.Message; }
            try { BeginInvoke((Action)(()=> {
                rendering=false;
                if(closing||Bounds!=snapshot||Strength!=blur||Dim!=dim) { if(next!=null)next.Dispose();return; }
                if(next!=null) { var old=frame;frame=next;if(old!=null)old.Dispose();FrameCount++;LastError=null; }
                else { if(frame!=null){frame.Dispose();frame=null;} LastError="模糊暂不可用，已用纯色遮挡："+error; }
                Invalidate();
            })); } catch { if(next!=null)next.Dispose(); }
        });
    }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(BackColor);
        if(frame!=null) { e.Graphics.InterpolationMode=InterpolationMode.HighQualityBilinear;e.Graphics.DrawImage(frame,ClientRectangle); }
        if(!Locked) {
            using(var pen=new Pen(Color.FromArgb(127,231,198),2)) e.Graphics.DrawRectangle(pen,1,1,Math.Max(1,Width-3),Math.Max(1,Height-3));
            if(Width>280&&Height>=55) {
                using(var brush=new SolidBrush(Color.FromArgb(180,18,30,28)))e.Graphics.FillRectangle(brush,6,6,242,25);
                TextRenderer.DrawText(e.Graphics,"拖动移动 · 边缘缩放 · 双击设置",Font,new Point(12,10),Color.White);
            }
            using(var p=new Pen(Color.White,1))for(int i=0;i<3;i++)e.Graphics.DrawLine(p,Width-6-i*5,Height-6,Width-6,Height-6-i*5);
        }
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { closing=true;timer.Stop();timer.Dispose();if(frame!=null)frame.Dispose();base.OnFormClosed(e); }
}

public class ControlPanel : Form {
    public readonly BlurOverlay Overlay;
    public bool PersistSettings=true;
    private readonly Settings settings;
    private NumericUpDown width,height;
    private TrackBar blur,dim;
    private Label blurValue,dimValue,status;
    private CheckBox locked;
    private Button show;
    private NotifyIcon tray;
    private bool syncing,exiting;
    private readonly System.Windows.Forms.Timer saveTimer=new System.Windows.Forms.Timer(),statusTimer=new System.Windows.Forms.Timer();
    public string HotkeyWarning="";
    private static readonly Color Bg=Color.FromArgb(23,33,34),Ink=Color.FromArgb(233,241,234),Muted=Color.FromArgb(160,181,176),Accent=Color.FromArgb(146,226,193);
    public ControlPanel() {
        settings=Settings.Load();Overlay=new BlurOverlay();Overlay.Bounds=new Rectangle(settings.X,settings.Y,settings.Width,settings.Height);Overlay.Strength=settings.Blur;Overlay.Dim=settings.Dim;
        Text="字幕遮挡器";Icon=SystemIcons.Application;AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Microsoft YaHei UI",10);BackColor=Bg;ForeColor=Ink;ClientSize=new Size(420,548);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.Manual;TopMost=true;
        var a=Screen.PrimaryScreen.WorkingArea;Location=new Point(a.Right-Width-28,a.Top+50);
        AddLabel("字幕遮挡器",22,20,370,34,19,Ink);AddLabel("把矩形框拖到字幕上，调整后锁定即可。",24,63,372,24,10,Muted);
        AddLabel("宽度（像素）",24,106,175,25,10,Muted);AddLabel("高度（像素）",220,106,175,25,10,Muted);
        width=Number(24,135,176,120,3840,settings.Width);height=Number(220,135,176,32,2160,settings.Height);
        AddLabel("高斯模糊",24,189,170,24,11,Ink);blurValue=AddLabel(settings.Blur.ToString(),330,189,66,24,11,Accent);blurValue.TextAlign=ContentAlignment.MiddleRight;
        blur=Slider(24,218,372,1,48,settings.Blur);
        AddLabel("加深遮挡",24,273,170,24,11,Ink);dimValue=AddLabel(settings.Dim+"%",330,273,66,24,11,Accent);dimValue.TextAlign=ContentAlignment.MiddleRight;
        dim=Slider(24,302,372,0,100,settings.Dim);
        locked=new CheckBox{Text="锁定位置，鼠标可穿透",Location=new Point(24,356),Size=new Size(360,30),ForeColor=Ink,Checked=settings.Locked};Controls.Add(locked);
        show=MakeButton("暂时隐藏",24,404,176);var center=MakeButton("移到屏幕下方",220,404,176);
        AddLabel("Ctrl + Alt + L  锁定 / 解锁\nCtrl + Alt + H  显示 / 隐藏\nCtrl + Alt + S  打开设置",24,462,374,67,9,Muted);
        status=AddLabel("",24,532,374,38,9,Accent);ClientSize=new Size(420,578);
        width.ValueChanged+=(s,e)=>ResizeOverlay();height.ValueChanged+=(s,e)=>ResizeOverlay();
        blur.ValueChanged+=(s,e)=>{Overlay.Strength=blur.Value;settings.Blur=blur.Value;blurValue.Text=blur.Value.ToString();ScheduleSave();};
        dim.ValueChanged+=(s,e)=>{Overlay.Dim=dim.Value;settings.Dim=dim.Value;dimValue.Text=dim.Value+"%";ScheduleSave();};
        locked.CheckedChanged+=(s,e)=>{Overlay.SetLocked(locked.Checked);settings.Locked=locked.Checked;ScheduleSave();};
        show.Click+=(s,e)=>ToggleVisible();center.Click+=(s,e)=>{var area=Screen.FromPoint(Cursor.Position).WorkingArea;Overlay.Size=new Size(Math.Min(Overlay.Width,area.Width-20),Math.Min(Overlay.Height,area.Height-20));Overlay.Location=new Point(area.Left+(area.Width-Overlay.Width)/2,area.Bottom-Overlay.Height-60);if(!Overlay.Visible)ToggleVisible();};
        Overlay.GeometryChanged+=SyncGeometry;Overlay.RequestSettings+=ShowControls;Overlay.RequestLock+=()=>locked.Checked=!locked.Checked;
        saveTimer.Interval=700;saveTimer.Tick+=(s,e)=>{saveTimer.Stop();Save();};
        statusTimer.Interval=1000;statusTimer.Tick+=(s,e)=>{status.Text=Overlay.LastError??(HotkeyWarning!=""?HotkeyWarning:!Overlay.Visible?"遮挡已隐藏":locked.Checked?"已锁定 · 点击会穿透遮挡框":"可拖动和缩放 · 设置会自动保存");};
        tray=new NotifyIcon{Icon=Icon,Text="字幕遮挡器",Visible=true};tray.DoubleClick+=(s,e)=>ShowControls();var menu=new ContextMenuStrip();menu.Items.Add("打开设置",null,(s,e)=>ShowControls());menu.Items.Add("显示 / 隐藏",null,(s,e)=>ToggleVisible());menu.Items.Add("锁定 / 解锁",null,(s,e)=>locked.Checked=!locked.Checked);menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,(s,e)=>Close());tray.ContextMenuStrip=menu;
        Resize+=(s,e)=>{if(WindowState==FormWindowState.Minimized)Hide();};
    }
    private Label AddLabel(string text,int x,int y,int w,int h,float size,Color color) {var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Microsoft YaHei UI",size),ForeColor=color};Controls.Add(l);return l;}
    private NumericUpDown Number(int x,int y,int w,int min,int max,int value) {var n=new NumericUpDown{Location=new Point(x,y),Size=new Size(w,34),Minimum=min,Maximum=max,Value=value,Increment=10,BackColor=Color.FromArgb(36,49,49),ForeColor=Ink,BorderStyle=BorderStyle.FixedSingle};Controls.Add(n);return n;}
    private TrackBar Slider(int x,int y,int w,int min,int max,int value) {var t=new TrackBar{Location=new Point(x,y),Size=new Size(w,40),Minimum=min,Maximum=max,Value=value,TickStyle=TickStyle.None,SmallChange=1,LargeChange=5};Controls.Add(t);return t;}
    private Button MakeButton(string text,int x,int y,int w) {var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(w,40),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(42,62,56),ForeColor=Ink};b.FlatAppearance.BorderColor=Color.FromArgb(79,109,96);Controls.Add(b);return b;}
    protected override void OnShown(EventArgs e) {
        base.OnShown(e);Overlay.Show();Overlay.SetLocked(locked.Checked);BringToFront();statusTimer.Start();
        bool ok=true;ok&=Native.RegisterHotKey(Handle,1,0x4003,(uint)Keys.L);ok&=Native.RegisterHotKey(Handle,2,0x4003,(uint)Keys.H);ok&=Native.RegisterHotKey(Handle,3,0x4003,(uint)Keys.S);if(!ok)HotkeyWarning="部分快捷键被占用，请使用托盘菜单。";
    }
    protected override void WndProc(ref Message m) {if(m.Msg==0x312) {int id=m.WParam.ToInt32();if(id==1)locked.Checked=!locked.Checked;else if(id==2)ToggleVisible();else if(id==3)ShowControls();return;}base.WndProc(ref m);}
    private void ResizeOverlay() {if(syncing)return;Overlay.Size=new Size((int)width.Value,(int)height.Value);}
    private void SyncGeometry() {if(width==null||height==null)return;syncing=true;width.Value=Math.Max(width.Minimum,Math.Min(width.Maximum,Overlay.Width));height.Value=Math.Max(height.Minimum,Math.Min(height.Maximum,Overlay.Height));syncing=false;settings.X=Overlay.Left;settings.Y=Overlay.Top;settings.Width=Overlay.Width;settings.Height=Overlay.Height;ScheduleSave();}
    private void ScheduleSave() {if(exiting)return;saveTimer.Stop();saveTimer.Start();}
    private void Save() {if(!PersistSettings)return;try {settings.Save();}catch {status.Text="无法保存设置，请检查程序文件夹的写入权限。";}}
    public void ToggleVisible() {if(Overlay.Visible)Overlay.Hide();else Overlay.Show();show.Text=Overlay.Visible?"暂时隐藏":"显示遮挡框";}
    public void ShowControls() {Show();WindowState=FormWindowState.Normal;Activate();}
    protected override void OnFormClosing(FormClosingEventArgs e) {exiting=true;saveTimer.Stop();statusTimer.Stop();Save();for(int i=1;i<=3;i++)Native.UnregisterHotKey(Handle,i);tray.Visible=false;tray.Dispose();Overlay.Close();saveTimer.Dispose();statusTimer.Dispose();base.OnFormClosing(e);}
}

static class Program {
    [STAThread] static void Main() {
        bool first;using(var mutex=new Mutex(true,"Local\\SubtitleBlur_ZhuanZ_v1",out first)) {
            if(!first){MessageBox.Show("字幕遮挡器已经在运行。\n请双击系统托盘图标，或按 Ctrl + Alt + S 打开设置。","字幕遮挡器");return;}
            Native.Dpi();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException+=(s,e)=>MessageBox.Show("遇到问题："+e.Exception.Message,"字幕遮挡器");
            Application.Run(new ControlPanel());
        }
    }
}
}
