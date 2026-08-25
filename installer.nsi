; Kulka Client Installer
; NSIS Script

!include "MUI2.nsh"
!include "FileFunc.nsh"

; ------------------------------------------------------------------ General
Name "Kulka Client"
OutFile "C:\Users\lucac\Desktop\KulkaClient-v1.1.1-setup.exe"
InstallDir "$LOCALAPPDATA\KulkaClient"
InstallDirRegKey HKCU "Software\KulkaClient" "InstallDir"
RequestExecutionLevel user
Unicode True

; ------------------------------------------------------------------ Version
VIProductVersion "1.1.1.0"
VIAddVersionKey "ProductName" "Kulka Client"
VIAddVersionKey "FileDescription" "Kulka Client Installer"
VIAddVersionKey "LegalCopyright" "PlanetSchach"
VIAddVersionKey "FileVersion" "1.1.1"

; ------------------------------------------------------------------ Pages
!define MUI_ABORTWARNING
!define MUI_ICON "src\MeinLauncher\Assets\kulkaclient.ico"
!define MUI_UNICON "src\MeinLauncher\Assets\kulkaclient.ico"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "German"
!insertmacro MUI_LANGUAGE "English"

; ------------------------------------------------------------------ Installer
Section "Install"
    SetOutPath "$INSTDIR"

    ; All files from publish directory
    File /r "src\MeinLauncher\bin\Release\net10.0\win-x64\publish\*.*"

    ; Store install path
    WriteRegStr HKCU "Software\KulkaClient" "InstallDir" "$INSTDIR"

    ; Uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"

    ; Add to Add/Remove Programs
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KulkaClient" \
        "DisplayName" "Kulka Client"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KulkaClient" \
        "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KulkaClient" \
        "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KulkaClient" \
        "DisplayVersion" "1.1.1"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KulkaClient" \
        "Publisher" "PlanetSchach"
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KulkaClient" \
        "NoModify" 1
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KulkaClient" \
        "NoRepair" 1

    ; Desktop Shortcut
    CreateShortcut "$DESKTOP\Kulka Client.lnk" "$INSTDIR\KulkaClient.exe" "" "$INSTDIR\KulkaClient.exe" 0

    ; Start Menu Entry
    CreateDirectory "$SMPROGRAMS\Kulka Client"
    CreateShortcut "$SMPROGRAMS\Kulka Client\Kulka Client.lnk" "$INSTDIR\KulkaClient.exe" "" "$INSTDIR\KulkaClient.exe" 0
    CreateShortcut "$SMPROGRAMS\Kulka Client\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
SectionEnd

; ------------------------------------------------------------------ Uninstaller
Section "Uninstall"
    ; Remove files
    RMDir /r "$INSTDIR"

    ; Remove shortcuts
    Delete "$DESKTOP\Kulka Client.lnk"
    RMDir /r "$SMPROGRAMS\Kulka Client"

    ; Remove registry keys
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KulkaClient"
    DeleteRegKey HKCU "Software\KulkaClient"
SectionEnd
