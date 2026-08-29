Option Explicit

Dim shell, fileSystem, scr, rundll32
Set shell = CreateObject("WScript.Shell")
Set fileSystem = CreateObject("Scripting.FileSystemObject")

scr = shell.ExpandEnvironmentStrings("%LOCALAPPDATA%\Programs\Aerial Screen Saver\Aerial.scr")
If Not fileSystem.FileExists(scr) Then
	MsgBox "Screensaver not found: " & scr, vbExclamation, "Aerial"
	WScript.Quit 1
End If

shell.RegWrite "HKCU\Control Panel\Desktop\SCRNSAVE.EXE", scr, "REG_SZ"
shell.RegWrite "HKCU\Control Panel\Desktop\ScreenSaveActive", 1, "REG_DWORD"
shell.RegWrite "HKCU\Control Panel\Desktop\ScreenSaveTimeOut", "300", "REG_SZ"

rundll32 = shell.ExpandEnvironmentStrings("%SystemRoot%\System32\rundll32.exe")
shell.Run """" & rundll32 & """ user32.dll,UpdatePerUserSystemParameters", 0, True
shell.Run """" & rundll32 & """ shell32.dll,Control_RunDLL desk.cpl,,1", 0, False
