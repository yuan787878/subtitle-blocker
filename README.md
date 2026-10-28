# 字幕遮挡器 / Subtitle Blocker

用于外语听力练习的字幕遮挡工具，包含 Windows 与 Android 两个独立实现。离线运行。

## 功能

- Windows：可拖动、缩放的实时高斯模糊框；纯黑遮挡；锁定与鼠标穿透；托盘和快捷键。
- Android：悬浮遮挡框，分别记忆横竖屏位置；Android 12 及以上在设备支持时使用系统模糊，否则使用纯色遮挡。

## Windows

建议 Windows 10 2004 或更高版本，需 .NET Framework 4.x。源代码使用 C# 和 Windows Forms。

在 Windows PowerShell 中运行：

```powershell
cd windows
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\dist\SubtitleBlocker.exe
```

拖动遮挡框到字幕区域，在设置中调节尺寸、模糊强度和加深程度。

- Ctrl + Alt + L：锁定 / 解锁
- Ctrl + Alt + H：显示 / 隐藏
- Ctrl + Alt + S：打开设置

设置保存在程序旁的 settings.xml，请将程序放在可写目录。独占全屏需改用窗口或无边框全屏；受保护的视频可能无法捕获。

## Android

Android 8.0 及以上。需要授予悬浮窗权限。源码及 Windows 构建说明见 [android/README.md](android/README.md)。需要 Python 3 和 JDK；首次构建会下载 Google 官方 SDK 工具。

每个开发者的构建会使用自己的本地签名密钥。本仓库不包含原安装包的签名私钥，自行构建的版本可能无法直接覆盖原安装包。

安卓版尚未进行真机或模拟器验证；系统模糊效果取决于设备支持情况。

## 测试

Windows 的已有测试源码位于 windows/tests/Tests.cs，包含需要图形桌面的界面测试。Android 几何测试不需要 Android 设备：

```powershell
cd android
javac -d work/test-classes app/src/main/java/cn/local/subtitleblur/Geometry.java tests/GeometryTest.java
java -cp work/test-classes GeometryTest
```

## 隐私

Windows 在内存中处理遮挡区域附近的屏幕图像，不保存或上传画面。Android 使用系统悬浮窗口，不申请网络、录屏或无障碍权限。

## 许可证

[MIT](LICENSE)。欢迎提交问题和改进。
