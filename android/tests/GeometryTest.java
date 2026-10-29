import cn.local.subtitleblur.Geometry;
public final class GeometryTest {
    private static void check(boolean condition,String message){if(!condition)throw new AssertionError(message);}
    public static void main(String[] args){
        int total=0;
        int[][] devices={{320,568},{360,800},{393,873},{600,960},{800,1280},{1280,800},{1920,1200},{2560,1600}};
        for(int[] d:devices)for(int orientation=0;orientation<2;orientation++)for(int density=1;density<=4;density++)for(int percent:new int[]{15,30,70,86,100})for(int height:new int[]{32,64,110,280})for(float x:new float[]{-1,0,.5f,1,2})for(float y:new float[]{-1,0,.82f,1,2}){
            int w=d[orientation],h=d[1-orientation];int[] b=Geometry.place(w,h,percent,height*density,96*density,32*density,x,y);
            check(b[0]>=0&&b[1]>=0&&b[2]>0&&b[3]>0,"negative geometry");
            check(b[0]+b[2]<=w&&b[1]+b[3]<=h,"off-screen after resize/rotation");
            check(Math.abs(Math.round(Geometry.normalize(b[0],w-b[2])*(w-b[2]))-b[0])<=1,"horizontal persistence mismatch");
            check(Math.abs(Math.round(Geometry.normalize(b[1],h-b[3])*(h-b[3]))-b[1])<=1,"vertical persistence mismatch");total++;
        }
        check(Geometry.unit(Float.NaN)==.5f,"invalid saved position");
        System.out.println("PASS: "+total+" handset/tablet, portrait/landscape, density, size and position scenarios.");
    }
}
