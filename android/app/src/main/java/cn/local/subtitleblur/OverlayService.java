package cn.local.subtitleblur;

import android.app.*;
import android.content.*;
import android.content.pm.ServiceInfo;
import android.content.res.Configuration;
import android.graphics.*;
import android.graphics.drawable.GradientDrawable;
import android.hardware.input.InputManager;
import android.hardware.display.DisplayManager;
import android.os.*;
import android.provider.Settings;
import android.util.DisplayMetrics;
import android.view.*;
import android.widget.TextView;
import android.widget.Toast;
import java.util.function.Consumer;

public final class OverlayService extends Service {
    public static final String START="start",UPDATE="update",TOGGLE_VISIBLE="visible",TOGGLE_LOCK="lock",STOP="stop";
    public static volatile boolean running=false,visible=true;
    private static final String CHANNEL="subtitle_overlay";
    private Context ui;
    private WindowManager manager;
    private Dialog dialog;
    private MaskView mask;
    private TextView bubble;
    private WindowManager.LayoutParams bubbleLayout;
    private Consumer<Boolean> blurListener;
    private Rect area;
    private boolean locked=false,blurEnabled=false;
    private final Handler handler=new Handler(Looper.getMainLooper());

    @Override public void onCreate(){
        super.onCreate();
        Display display=((DisplayManager)getSystemService(DISPLAY_SERVICE)).getDisplay(Display.DEFAULT_DISPLAY);
        Context displayContext=createDisplayContext(display);
        ui=Build.VERSION.SDK_INT>=30?displayContext.createWindowContext(WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,null):displayContext;
        manager=(WindowManager)ui.getSystemService(WINDOW_SERVICE);
        NotificationManager notifications=(NotificationManager)getSystemService(NOTIFICATION_SERVICE);
        notifications.createNotificationChannel(new NotificationChannel(CHANNEL,"字幕遮挡控制",NotificationManager.IMPORTANCE_LOW));
    }
    @Override public int onStartCommand(Intent intent,int flags,int startId){
        String action=intent==null?START:intent.getAction();
        if(STOP.equals(action)){stopSelf();return START_NOT_STICKY;}
        if(!Settings.canDrawOverlays(this)){stopSelf();return START_NOT_STICKY;}
        try{
            Notification n=notification();
            if(Build.VERSION.SDK_INT>=34)startForeground(17,n,ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE);else startForeground(17,n);
            if(dialog==null){visible=true;createWindows();}
            running=true;
            if(TOGGLE_LOCK.equals(action))Prefs.set(this,"locked",!Prefs.get(this).getBoolean("locked",false));
            if(TOGGLE_VISIBLE.equals(action))visible=!visible;
            apply();
            ((NotificationManager)getSystemService(NOTIFICATION_SERVICE)).notify(17,notification());
        }catch(Exception e){Toast.makeText(this,"悬浮框暂时无法开启，请检查悬浮窗权限。",Toast.LENGTH_LONG).show();stopSelf();}
        return START_NOT_STICKY;
    }
    private int dp(int n){return Math.round(n*ui.getResources().getDisplayMetrics().density);}
    private Rect screenArea(){
        if(Build.VERSION.SDK_INT>=30){WindowMetrics metrics=manager.getCurrentWindowMetrics();Rect b=new Rect(metrics.getBounds());Insets i=metrics.getWindowInsets().getInsetsIgnoringVisibility(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());b.left+=i.left;b.top+=i.top;b.right-=i.right;b.bottom-=i.bottom;return b;}
        DisplayMetrics m=new DisplayMetrics();manager.getDefaultDisplay().getRealMetrics(m);int top=0,bottom=0;
        int id=getResources().getIdentifier("status_bar_height","dimen","android");if(id>0)top=getResources().getDimensionPixelSize(id);
        id=getResources().getIdentifier("navigation_bar_height","dimen","android");if(id>0)bottom=getResources().getDimensionPixelSize(id);
        return new Rect(0,top,m.widthPixels,m.heightPixels-bottom);
    }
    private void createWindows(){
        area=screenArea();
        dialog=new Dialog(ui,R.style.OverlayTheme);dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);dialog.setCancelable(false);
        Window window=dialog.getWindow();window.setType(WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY);
        window.addFlags(WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE|WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL|WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN|WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
        window.clearFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);window.setDimAmount(0);
        mask=new MaskView(ui);dialog.setContentView(mask);window.getDecorView().setPadding(0,0,0,0);
        if(Build.VERSION.SDK_INT>=31){blurEnabled=manager.isCrossWindowBlurEnabled();blurListener=enabled->{blurEnabled=enabled;if(dialog!=null)apply();};manager.addCrossWindowBlurEnabledListener(getMainExecutor(),blurListener);}
        dialog.show();
        bubble=new TextView(ui);bubble.setText("字幕\n设置");bubble.setTextColor(0xffd5f4df);bubble.setTextSize(12);bubble.setGravity(Gravity.CENTER);bubble.setContentDescription("打开字幕遮挡设置，拖动可移动按钮");
        GradientDrawable bd=new GradientDrawable();bd.setColor(0xee243a2e);bd.setCornerRadius(dp(16));bd.setStroke(dp(1),0xff9bccad);bubble.setBackground(bd);
        bubbleLayout=new WindowManager.LayoutParams(dp(52),dp(52),WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE|WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL|WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN,PixelFormat.TRANSLUCENT);
        bubbleLayout.gravity=Gravity.TOP|Gravity.LEFT;bubbleLayout.x=area.right-dp(62);bubbleLayout.y=area.top+dp(30);
        bubble.setOnClickListener(v->openSettings());
        bubble.setOnTouchListener(new View.OnTouchListener(){float x,y;int ox,oy;boolean moved;public boolean onTouch(View v,MotionEvent e){switch(e.getActionMasked()){
            case MotionEvent.ACTION_DOWN:x=e.getRawX();y=e.getRawY();ox=bubbleLayout.x;oy=bubbleLayout.y;moved=false;return true;
            case MotionEvent.ACTION_MOVE:if(Math.abs(e.getRawX()-x)+Math.abs(e.getRawY()-y)>dp(6))moved=true;if(moved){bubbleLayout.x=Geometry.clamp(ox+Math.round(e.getRawX()-x),area.left,area.right-bubbleLayout.width);bubbleLayout.y=Geometry.clamp(oy+Math.round(e.getRawY()-y),area.top,area.bottom-bubbleLayout.height);manager.updateViewLayout(bubble,bubbleLayout);}return true;
            case MotionEvent.ACTION_UP:if(!moved)v.performClick();return true;
            case MotionEvent.ACTION_CANCEL:return true;
        }return false;}});
        manager.addView(bubble,bubbleLayout);
    }
    private void apply(){
        if(dialog==null)return;
        area=screenArea();String o=Prefs.orientation(this);android.content.SharedPreferences p=Prefs.get(this);
        int[] box=Geometry.place(area.width(),area.height(),Prefs.width(this),dp(Prefs.height(this)),dp(96),dp(32),p.getFloat("x_"+o,.5f),p.getFloat("y_"+o,.82f));
        Window w=dialog.getWindow();WindowManager.LayoutParams a=w.getAttributes();a.gravity=Gravity.TOP|Gravity.LEFT;a.x=area.left+box[0];a.y=area.top+box[1];a.width=box[2];a.height=box[3];a.format=PixelFormat.TRANSLUCENT;
        locked=p.getBoolean("locked",false);
        if(locked)a.flags|=WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE;else a.flags&=~WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE;
        a.alpha=1f;
        if(locked&&Build.VERSION.SDK_INT>=31){InputManager input=(InputManager)getSystemService(INPUT_SERVICE);a.alpha=Math.max(.1f,Math.min(.79f,input.getMaximumObscuringOpacityForTouch()-.01f));}
        boolean useBlur=Build.VERSION.SDK_INT>=31&&blurEnabled&&!p.getBoolean("solid",false);
        GradientDrawable background=new GradientDrawable();background.setCornerRadius(0);int shade=Geometry.clamp(p.getInt("shade",30),0,100);background.setColor(useBlur?Color.argb(Math.round(255*shade/100f),16,22,19):Color.rgb(16,22,19));
        w.setBackgroundDrawable(background);w.setAttributes(a);
        if(Build.VERSION.SDK_INT>=31)w.setBackgroundBlurRadius(useBlur?Geometry.clamp(p.getInt("blur",80),1,150):0);
        if(visible){if(!dialog.isShowing())dialog.show();}else dialog.hide();
        mask.invalidate();
        if(bubble!=null){bubbleLayout.x=Geometry.clamp(bubbleLayout.x,area.left,area.right-bubbleLayout.width);bubbleLayout.y=Geometry.clamp(bubbleLayout.y,area.top,area.bottom-bubbleLayout.height);manager.updateViewLayout(bubble,bubbleLayout);}
    }
    private void saveGeometry(){
        WindowManager.LayoutParams a=dialog.getWindow().getAttributes();String o=Prefs.orientation(this);
        Prefs.get(this).edit().putInt("width_"+o,Geometry.clamp(Math.round(a.width*100f/area.width()),15,100)).putInt("height_"+o,Geometry.clamp(Math.round(a.height/ui.getResources().getDisplayMetrics().density),32,280)).putFloat("x_"+o,Geometry.normalize(a.x-area.left,area.width()-a.width)).putFloat("y_"+o,Geometry.normalize(a.y-area.top,area.height()-a.height)).apply();
    }
    private void openSettings(){try{startActivity(new Intent(this,MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK|Intent.FLAG_ACTIVITY_SINGLE_TOP));}catch(Exception e){Toast.makeText(this,"请点击桌面的字幕遮挡器打开设置。",Toast.LENGTH_SHORT).show();}}
    private PendingIntent action(String action,int request){return PendingIntent.getService(this,request,new Intent(this,OverlayService.class).setAction(action),PendingIntent.FLAG_UPDATE_CURRENT|PendingIntent.FLAG_IMMUTABLE);}
    private Notification notification(){
        PendingIntent open=PendingIntent.getActivity(this,0,new Intent(this,MainActivity.class),PendingIntent.FLAG_UPDATE_CURRENT|PendingIntent.FLAG_IMMUTABLE);
        boolean lock=Prefs.get(this).getBoolean("locked",false);
        return new Notification.Builder(this,CHANNEL).setSmallIcon(R.drawable.ic_overlay).setContentTitle("字幕遮挡器运行中").setContentText("点击打开设置；可随时停止遮挡。").setContentIntent(open).setOngoing(true).setOnlyAlertOnce(true)
            .addAction(new Notification.Action.Builder(null,lock?"解锁":"锁定",action(TOGGLE_LOCK,1)).build())
            .addAction(new Notification.Action.Builder(null,visible?"隐藏":"显示",action(TOGGLE_VISIBLE,2)).build())
            .addAction(new Notification.Action.Builder(null,"停止",action(STOP,3)).build()).build();
    }
    @Override public void onConfigurationChanged(Configuration c){super.onConfigurationChanged(c);handler.postDelayed(()->{if(dialog!=null)try{apply();}catch(Exception e){stopSelf();}},180);}
    @Override public void onDestroy(){
        running=false;visible=true;handler.removeCallbacksAndMessages(null);
        if(Build.VERSION.SDK_INT>=31&&blurListener!=null)try{manager.removeCrossWindowBlurEnabledListener(blurListener);}catch(Exception ignored){}
        if(dialog!=null){try{dialog.dismiss();}catch(Exception ignored){}dialog=null;}
        if(bubble!=null){try{manager.removeView(bubble);}catch(Exception ignored){}bubble=null;}
        stopForeground(STOP_FOREGROUND_REMOVE);super.onDestroy();
    }
    @Override public IBinder onBind(Intent intent){return null;}
    private final class MaskView extends View {
        final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);float sx,sy;int ox,oy,ow,oh;boolean resize;
        MaskView(Context c){super(c);setContentDescription("字幕遮挡框，拖动移动，右下角缩放");setLayerType(View.LAYER_TYPE_HARDWARE,null);}
        @Override protected void onDraw(Canvas canvas){
            super.onDraw(canvas);if(locked)return;
            paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(dp(2));paint.setColor(0xffa6e3c4);canvas.drawRect(dp(1),dp(1),getWidth()-dp(1),getHeight()-dp(1),paint);paint.setStyle(Paint.Style.FILL);
            paint.setTextSize(dp(12));paint.setColor(Color.WHITE);if(getWidth()>dp(220))canvas.drawText("拖动移动 · 右下角缩放",dp(10),Math.min(getHeight()-dp(9),dp(23)),paint);
            paint.setStrokeWidth(dp(2));for(int i=0;i<3;i++)canvas.drawLine(getWidth()-dp(7+i*6),getHeight()-dp(7),getWidth()-dp(7),getHeight()-dp(7+i*6),paint);
        }
        @Override public boolean onTouchEvent(MotionEvent e){
            if(locked)return false;if(e.getPointerCount()>1)return true;
            WindowManager.LayoutParams a=dialog.getWindow().getAttributes();
            switch(e.getActionMasked()){
                case MotionEvent.ACTION_DOWN:sx=e.getRawX();sy=e.getRawY();ox=a.x;oy=a.y;ow=a.width;oh=a.height;resize=e.getX()>getWidth()-dp(40)&&e.getY()>getHeight()-dp(40);return true;
                case MotionEvent.ACTION_MOVE:int dx=Math.round(e.getRawX()-sx),dy=Math.round(e.getRawY()-sy);if(resize){a.width=Geometry.clamp(ow+dx,Math.min(Math.max(dp(96),Math.round(area.width()*.15f)),area.right-a.x),area.right-a.x);a.height=Geometry.clamp(oh+dy,Math.min(dp(32),area.bottom-a.y),Math.min(dp(280),area.bottom-a.y));}else{a.x=Geometry.clamp(ox+dx,area.left,area.right-a.width);a.y=Geometry.clamp(oy+dy,area.top,area.bottom-a.height);}dialog.getWindow().setAttributes(a);return true;
                case MotionEvent.ACTION_UP:saveGeometry();performClick();return true;
                case MotionEvent.ACTION_CANCEL:saveGeometry();return true;
            }return super.onTouchEvent(e);
        }
        @Override public boolean performClick(){super.performClick();return true;}
    }
}
