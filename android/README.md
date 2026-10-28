# 字幕遮挡器 · Android

原生 Java 安卓应用，minSdk 26、targetSdk / compileSdk 35。适配手机、平板与横竖屏；本地运行，无网络、录屏或无障碍权限。

安装包在 `安装包/字幕遮挡器-Android.apk`。操作和兼容说明见同目录的 `安装与使用说明.txt`。

## 实现

- 使用 `TYPE_APPLICATION_OVERLAY` 与前台 `specialUse` 服务维持用户主动开启的遮挡框。
- API 31+ 使用 `Window.setBackgroundBlurRadius`，只模糊矩形窗范围；监听系统模糊开关。不支持时使用纯色背景。
- 锁定时按 Android 的触控遮挡上限设置窗口透明度，避免框下应用的输入被系统阻止。
- 以可用屏幕的相对坐标保存位置，横竖屏分别存储；避开状态栏、导航栏和刘海区域。
- 平板宽屏双栏，小屏单栏；无固定屏幕方向，无原生 CPU 架构依赖。

## 在 Windows 重新构建

需要 Python 3 和 JDK（`java`、`javac`、`jar`、`keytool` 在 PATH 中）：

```powershell
python build.py
```

构建脚本下载并校验 Google 官方 Android 35 平台与 Build Tools 35.0.0；全部存放在项目 `work/` 内。首次需联网，之后可以离线重建。

本地签名密钥位于 `work/local-signing.p12`。为继续覆盖安装更新，请保留此文件；源码压缩包不包含它。此版本为本地测试安装包，未发布应用商店。

## 验证

已执行 Java 编译、D8 转换、APK v2/v3 签名检查、ZIP 对齐检查和几何测试。没有真机或模拟器测试结果，不据此宣称所有厂商机型都能使用系统模糊。

## 官方资料

- [Android 跨窗口模糊](https://source.android.com/docs/core/display/window-blurs)
- [Android 12 悬浮窗触摸限制](https://developer.android.com/about/versions/12/behavior-changes-all#untrusted-touch-events)
- [前台服务 specialUse 类型](https://developer.android.com/develop/background-work/services/fgs/service-types#special-use)
