package cn.local.subtitleblur;

/** Pure geometry, shared by handset/tablet and tested without Android. */
public final class Geometry {
    public static int clamp(int value,int low,int high){return Math.max(low,Math.min(Math.max(low,high),value));}
    public static float unit(float value){return Float.isNaN(value)?0.5f:Math.max(0f,Math.min(1f,value));}
    public static int[] place(int aw,int ah,int widthPercent,int heightPx,int minWidth,int minHeight,float nx,float ny){
        int w=clamp(Math.round(aw*widthPercent/100f),Math.min(minWidth,aw),aw);
        int h=clamp(heightPx,Math.min(minHeight,ah),ah);
        return new int[]{Math.round((aw-w)*unit(nx)),Math.round((ah-h)*unit(ny)),w,h};
    }
    public static float normalize(int offset,int free){return free<=0?0.5f:unit(offset/(float)free);}
    private Geometry(){}
}
