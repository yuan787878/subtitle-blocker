package cn.local.subtitleblur;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.graphics.Insets;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.view.Gravity;
import android.view.View;
import android.view.WindowInsets;
import android.widget.*;

public final class MainActivity extends Activity {
    private static final int GREEN=0xff28654f,INK=0xff22372e,MUTED=0xff617368;
    private TextView status;
    private Button start,visibility;
    private boolean pendingStart;
    private final Handler handler=new Handler(Looper.getMainLooper());
    private final Runnable refresh=new Runnable(){public void run(){updateStatus();handler.postDelayed(this,800);}};
    @Override public void onCreate(Bundle state){super.onCreate(state);if(state!=null)pendingStart=state.getBoolean("pendingStart");build();}
    @Override protected void onSaveInstanceState(Bundle state){super.onSaveInstanceState(state);state.putBoolean("pendingStart",pendingStart);}
    private int dp(int n){return Math.round(n*getResources().getDisplayMetrics().density);}
    private GradientDrawable background(int color,int radius){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(radius));return d;}
    private TextView text(String value,int size,int color){TextView v=new TextView(this);v.setText(value);v.setTextSize(size);v.setTextColor(color);v.setLineSpacing(dp(3),1);return v;}
    private LinearLayout column(){LinearLayout l=new LinearLayout(this);l.setOrientation(LinearLayout.VERTICAL);return l;}
    private void gap(LinearLayout l,int height){Space v=new Space(this);l.addView(v,new LinearLayout.LayoutParams(1,dp(height)));}
    private LinearLayout card(){LinearLayout c=column();c.setPadding(dp(20),dp(20),dp(20),dp(20));c.setBackground(background(Color.WHITE,20));return c;}
    private Button button(String title,boolean primary){Button b=new Button(this);b.setText(title);b.setAllCaps(false);b.setTextSize(16);b.setTextColor(primary?Color.WHITE:GREEN);b.setMinHeight(dp(52));b.setBackgroundTintList(android.content.res.ColorStateList.valueOf(primary?GREEN:0xffe3eee5));return b;}
    private interface Change{void apply(int value);}
    private void slider(LinearLayout parent,String title,int min,int max,int current,String suffix,Change change){
        TextView label=text(title+"  "+current+suffix,16,INK);parent.addView(label);
        SeekBar bar=new SeekBar(this);bar.setMax(max-min);bar.setProgress(Geometry.clamp(current,min,max)-min);bar.setContentDescription(title);parent.addView(bar,new LinearLayout.LayoutParams(-1,dp(48)));
        bar.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){public void onProgressChanged(SeekBar s,int p,boolean user){label.setText(title+"  "+(p+min)+suffix);if(user){change.apply(p+min);sendUpdate();}}public void onStartTrackingTouch(SeekBar s){}public void onStopTrackingTouch(SeekBar s){}});
        gap(parent,14);
    }
    private void build(){
        ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setBackgroundColor(0xfff4f5f0);
        LinearLayout outer=column();outer.setGravity(Gravity.TOP|Gravity.CENTER_HORIZONTAL);outer.setPadding(dp(20),dp(20),dp(20),dp(28));scroll.addView(outer);
        scroll.setOnApplyWindowInsetsListener((v,insets)->{int l,t,r,b;if(Build.VERSION.SDK_INT>=30){Insets i=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());l=i.left;t=i.top;r=i.right;b=i.bottom;}else{l=insets.getSystemWindowInsetLeft();t=insets.getSystemWindowInsetTop();r=insets.getSystemWindowInsetRight();b=insets.getSystemWindowInsetBottom();}outer.setPadding(dp(20)+l,dp(20)+t,dp(20)+r,dp(28)+b);return insets;});
        LinearLayout body=column();outer.addView(body,new LinearLayout.LayoutParams(-1,-2));
        outer.addOnLayoutChangeListener((v,l,t,r,b,ol,ot,or,ob)->{int available=Math.max(1,outer.getWidth()-outer.getPaddingLeft()-outer.getPaddingRight());int width=Math.min(dp(940),available);if(body.getLayoutParams().width!=width){LinearLayout.LayoutParams layout=(LinearLayout.LayoutParams)body.getLayoutParams();layout.width=width;body.setLayoutParams(layout);}});
        TextView title=text("字幕遮挡器",28,INK);title.setTypeface(null,Typeface.BOLD);body.addView(title);gap(body,6);body.addView(text("把字幕藏起来，专心看画面。",15,MUTED));gap(body,22);
        LinearLayout controls=card();status=text("正在检查设备…",15,GREEN);controls.addView(status);gap(controls,16);start=button("开启悬浮框",true);controls.addView(start,new LinearLayout.LayoutParams(-1,dp(56)));start.setOnClickListener(v->{if(OverlayService.running){stopService(new Intent(this,OverlayService.class));}else begin();handler.postDelayed(this::updateStatus,250);});
        visibility=button("暂时隐藏 / 显示",false);controls.addView(visibility,new LinearLayout.LayoutParams(-1,dp(56)));visibility.setOnClickListener(v->command(OverlayService.TOGGLE_VISIBLE));gap(controls,12);
        controls.addView(text("拖动框内空白处移动，拖右下角改变大小。点击浮动的「字幕」按钮可回到设置。",14,MUTED));gap(controls,18);
        Switch lock=new Switch(this);lock.setText("锁定位置并允许点击穿透");lock.setTextColor(INK);lock.setMinHeight(dp(52));lock.setChecked(Prefs.get(this).getBoolean("locked",false));controls.addView(lock);lock.setOnCheckedChangeListener((v,checked)->{Prefs.set(this,"locked",checked);sendUpdate();});
        controls.addView(text("穿透模式会按系统要求降低遮挡不透明度。若字幕仍可辨认，请解锁或切换纯色遮挡。",12,MUTED));gap(controls,14);
        Button reset=button("移回屏幕下方",false);controls.addView(reset,new LinearLayout.LayoutParams(-1,dp(52)));reset.setOnClickListener(v->{String o=Prefs.orientation(this);Prefs.get(this).edit().putFloat("x_"+o,.5f).putFloat("y_"+o,.82f).apply();sendUpdate();});
        LinearLayout adjust=card();TextView heading=text("尺寸与效果",20,INK);heading.setTypeface(null,Typeface.BOLD);adjust.addView(heading);gap(adjust,6);adjust.addView(text("横屏和竖屏分别记住位置与尺寸。",13,MUTED));gap(adjust,20);
        String orientation=Prefs.orientation(this);
        slider(adjust,"宽度",15,100,Prefs.width(this),"%",n->Prefs.set(this,"width_"+orientation,n));
        slider(adjust,"高度",32,280,Prefs.height(this)," dp",n->Prefs.set(this,"height_"+orientation,n));
        slider(adjust,"模糊强度",1,150,Prefs.get(this).getInt("blur",80),"",n->Prefs.set(this,"blur",n));
        slider(adjust,"模糊底色深度",0,100,Prefs.get(this).getInt("shade",30),"%",n->Prefs.set(this,"shade",n));
        Switch solid=new Switch(this);solid.setText("优先使用纯色遮挡");solid.setTextColor(INK);solid.setMinHeight(dp(48));solid.setChecked(Prefs.get(this).getBoolean("solid",false));adjust.addView(solid);solid.setOnCheckedChangeListener((v,c)->{Prefs.set(this,"solid",c);sendUpdate();updateStatus();});
        boolean wide=getResources().getConfiguration().screenWidthDp>=680;
        LinearLayout panels=new LinearLayout(this);panels.setOrientation(wide?LinearLayout.HORIZONTAL:LinearLayout.VERTICAL);
        if(wide){panels.addView(controls,new LinearLayout.LayoutParams(0,-2,1));Space s=new Space(this);panels.addView(s,new LinearLayout.LayoutParams(dp(18),1));panels.addView(adjust,new LinearLayout.LayoutParams(0,-2,1));}else{panels.addView(controls,new LinearLayout.LayoutParams(-1,-2));gap(panels,16);panels.addView(adjust,new LinearLayout.LayoutParams(-1,-2));}body.addView(panels);gap(body,20);
        body.addView(text("兼容说明",16,INK));gap(body,6);body.addView(text("安卓 12 及以上且设备支持时，使用系统背景模糊；其他情况自动使用纯色遮挡。省电模式也可能关闭系统模糊。\n\n首次使用需允许「显示在其他应用上层」。应用不录屏、不读取视频，不需要网络、相册或无障碍权限。若系统回收悬浮框，请重新打开应用启动。",13,MUTED));
        setContentView(scroll);updateStatus();
    }
    private void begin(){
        if(!Settings.canDrawOverlays(this)){pendingStart=true;try{startActivity(new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION,Uri.parse("package:"+getPackageName())));}catch(Exception e){pendingStart=false;Toast.makeText(this,"请在系统设置中允许本应用显示悬浮窗。",Toast.LENGTH_LONG).show();}return;}
        pendingStart=false;
        try{startForegroundService(new Intent(this,OverlayService.class).setAction(OverlayService.START));}catch(Exception e){Toast.makeText(this,"启动失败："+e.getMessage(),Toast.LENGTH_LONG).show();return;}
        if(Build.VERSION.SDK_INT>=33&&checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},1);
    }
    private void sendUpdate(){if(OverlayService.running)command(OverlayService.UPDATE);}
    private void command(String action){if(!OverlayService.running)return;try{startService(new Intent(this,OverlayService.class).setAction(action));}catch(Exception e){Toast.makeText(this,"请重新开启悬浮框。",Toast.LENGTH_SHORT).show();}}
    private void updateStatus(){if(status==null)return;boolean blur=Build.VERSION.SDK_INT>=31&&getWindowManager().isCrossWindowBlurEnabled()&&!Prefs.get(this).getBoolean("solid",false);String mode=blur?"系统背景模糊":"纯色兼容遮挡";String state=OverlayService.running?(OverlayService.visible?"运行中":"已暂时隐藏"):"未启动";status.setText(state+" · "+mode);start.setText(OverlayService.running?"停止遮挡":"开启悬浮框");visibility.setEnabled(OverlayService.running);}
    @Override protected void onResume(){super.onResume();build();if(pendingStart){pendingStart=false;if(Settings.canDrawOverlays(this))begin();}handler.removeCallbacks(refresh);handler.post(refresh);}
    @Override protected void onNewIntent(Intent intent){super.onNewIntent(intent);setIntent(intent);build();}
    @Override protected void onPause(){handler.removeCallbacks(refresh);super.onPause();}
}
