@echo off
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /codepage:65001 /out:PomodoroTimer.exe PomodoroTimer.cs
if %errorlevel%==0 echo Done: PomodoroTimer.exe
