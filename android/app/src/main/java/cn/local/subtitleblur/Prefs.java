package cn.local.subtitleblur;

import android.content.Context;
import android.content.SharedPreferences;

final class Prefs {
    static SharedPreferences get(Context c){return c.getSharedPreferences("subtitle_mask",Context.MODE_PRIVATE);}
    static String orientation(Context c){return c.getResources().getConfiguration().orientation==2?"land":"port";}
    static int width(Context c){String o=orientation(c);return get(c).getInt("width_"+o,o.equals("land")?70:86);}
    static int height(Context c){return get(c).getInt("height_"+orientation(c),64);}
    static void set(Context c,String key,int value){get(c).edit().putInt(key,value).apply();}
    static void set(Context c,String key,boolean value){get(c).edit().putBoolean(key,value).apply();}
    private Prefs(){}
}
