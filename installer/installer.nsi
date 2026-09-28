; OpenCode Tray - NSIS installer (per-user, no admin required)
Unicode true
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!define APP_NAME "OpenCode Tray"
!define APP_EXE "OpenCodeTray.exe"
!define APP_ID "OpenCodeTray"
!define APP_VERSION "1.2.0"
!define APP_PUBLISHER "michalkulik"
!define APP_URL "https://github.com/michalkulik/opencode-tray"
!define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"

Name "${APP_NAME}"
OutFile "..\dist\OpenCodeTray-Setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\${APP_NAME}"
InstallDirRegKey HKCU "Software\${APP_ID}" "InstallDir"
RequestExecutionLevel user

VIProductVersion "1.2.0.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "© ${APP_PUBLISHER}"

!define MUI_ICON "..\src\OpenCodeTray\Assets\icon.ico"
!define MUI_UNICON "..\src\OpenCodeTray\Assets\icon.ico"
!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Polish"
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ; Detect a previous per-machine install and fail clearly instead of mixing.
  ReadRegStr $0 HKLM "${UNINST_KEY}" "InstallLocation"
  StrCmp $0 "" checkRuntime
  MessageBox MB_OK|MB_ICONSTOP "OpenCode Tray jest już zainstalowany dla wszystkich użytkowników. Odinstaluj poprzednią wersję i spróbuj ponownie."
  Abort

checkRuntime:
  ; The app is framework-dependent: it needs .NET Desktop Runtime 8.0 or newer.
  Call HasDesktopRuntime
  StrCmp $R0 1 runtimeOk
  MessageBox MB_YESNO|MB_ICONINFORMATION "Na tym komputerze nie znaleziono .NET Desktop Runtime (8.0 lub nowszy), którego wymaga OpenCode Tray.$\n$\nBez niego aplikacja się nie uruchomi.$\n$\nKliknij „Tak”, aby otworzyć stronę pobrania runtime'u i kontynuować instalację, albo „Nie”, aby anulować." IDNO abortInstall
  ExecShell "open" "https://dotnet.microsoft.com/download/dotnet/8.0"
  Goto runtimeOk

abortInstall:
  Abort

runtimeOk:
FunctionEnd

; ---------------------------------------------------------------------------
; Returns $R0 = 1 when a supported Desktop Runtime is present for this machine.
; $PROGRAMFILES64\dotnet  -> machine-wide install
; $LOCALAPPDATA\Microsoft\dotnet -> per-user install
; ---------------------------------------------------------------------------
Function HasDesktopRuntime
  StrCpy $R0 0

  StrCpy $R1 "$PROGRAMFILES64\dotnet\shared\Microsoft.WindowsDesktop.App"
  Call CheckRuntimeDir
  StrCmp $R0 1 hasRuntime

  StrCpy $R1 "$LOCALAPPDATA\Microsoft\dotnet\shared\Microsoft.WindowsDesktop.App"
  Call CheckRuntimeDir

hasRuntime:
FunctionEnd

Function CheckRuntimeDir
  ${If} ${FileExists} "$R1\8.0.*"
    StrCpy $R0 1
  ${ElseIf} ${FileExists} "$R1\9.0.*"
    StrCpy $R0 1
  ${ElseIf} ${FileExists} "$R1\10.0.*"
    StrCpy $R0 1
  ${ElseIf} ${FileExists} "$R1\11.0.*"
    StrCpy $R0 1
  ${ElseIf} ${FileExists} "$R1\12.0.*"
    StrCpy $R0 1
  ${EndIf}
FunctionEnd

Function CloseRunningApp
  ; Close a running instance so the binary can be replaced or removed.
  nsExec::Exec 'taskkill /IM "${APP_EXE}" /F'
  Pop $0
  Sleep 400
FunctionEnd

Function un.CloseRunningApp
  nsExec::Exec 'taskkill /IM "${APP_EXE}" /F'
  Pop $0
  Sleep 400
FunctionEnd

Section "OpenCode Tray" SecMain
  SectionIn RO
  Call CloseRunningApp

  SetOutPath "$INSTDIR"
  File "..\src\OpenCodeTray\bin\publish\${APP_EXE}"

  WriteRegStr HKCU "Software\${APP_ID}" "InstallDir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Auto-start with Windows (points at the installed executable).
  WriteRegStr HKCU "${RUN_KEY}" "${APP_ID}" '"$INSTDIR\${APP_EXE}" --startup'

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "${UNINST_KEY}" "URLInfoAbout" "${APP_URL}"
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

Section "Uninstall"
  Call un.CloseRunningApp

  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"

  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  Delete "$DESKTOP\${APP_NAME}.lnk"

  DeleteRegValue HKCU "${RUN_KEY}" "${APP_ID}"
  DeleteRegKey HKCU "${UNINST_KEY}"
  DeleteRegKey HKCU "Software\${APP_ID}"

  ; Remove the user's settings only if they ask.
  MessageBox MB_YESNO|MB_ICONQUESTION "Czy usunąć również ustawienia i klucz API ($APPDATA\OpenCodeTray)?" IDNO skip
  RMDir /r "$APPDATA\OpenCodeTray"
skip:
SectionEnd
