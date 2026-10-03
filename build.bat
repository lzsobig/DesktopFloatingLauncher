@echo off
chcp 65001 >nul
echo ========================================================
echo   DesktopFloatingLauncher - 一键极速编译脚本
echo ========================================================
echo.

set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC_PATH%" (
    set "CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC_PATH%" (
    echo [错误] 未在系统中检测到 .NET Framework 4.0/4.8 编译器 (csc.exe)。
    pause
    exit /b 1
)

if not exist "bin" mkdir "bin"

echo 正在编译源码 (src\Program.cs)...
"%CSC_PATH%" /target:winexe /out:"bin\桌面极速悬浮窗.exe" /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll","C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll","C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll","System.Xaml.dll","System.dll","System.Core.dll","System.Data.dll","System.Drawing.dll","System.Windows.Forms.dll","Microsoft.CSharp.dll" src\Program.cs

if %errorlevel% neq 0 (
    echo.
    echo [失败] 编译失败，请检查上方错误提示。
    pause
    exit /b %errorlevel%
)

echo.
echo [成功] 编译完成！可执行文件路径：
echo   bin\桌面极速悬浮窗.exe
echo.
pause
