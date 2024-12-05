;--------------------------------
;Plugins
;https://nsis.sourceforge.io/ApplicationID_plug-in
;https://nsis.sourceforge.io/ShellExecAsUser_plug-in
;https://nsis.sourceforge.io/NsProcess_plugin
;https://nsis.sourceforge.io/Inetc_plug-in

;--------------------------------
;Version

    !define PRODUCT_VERSION "1.0.0.0"
    !define VERSION "1.0.0.0"
    VIProductVersion "${PRODUCT_VERSION}"
    VIFileVersion "${VERSION}"
    VIAddVersionKey "FileVersion" "${VERSION}"
    VIAddVersionKey "ProductName" "ShockOSC"
    VIAddVersionKey "ProductVersion" "${PRODUCT_VERSION}"
    VIAddVersionKey "LegalCopyright" "Copyright OpenShock"
    VIAddVersionKey "FileDescription" ""

;--------------------------------
;Include Modern UI

    !include "MUI2.nsh"
    !include "FileFunc.nsh"
    !include "LogicLib.nsh"

;--------------------------------
;General

    Unicode True
    Name "ShockOSC"
    OutFile "ShockOSC_Setup.exe"
    InstallDir "$LocalAppdata\ShockOSC"
    InstallDirRegKey HKLM "Software\ShockOSC" "InstallDir"
    RequestExecutionLevel admin
    ShowInstDetails show

;--------------------------------
;Variables

    VAR upgradeInstallation

;--------------------------------
;Interface Settings

    !define MUI_ABORTWARNING

;--------------------------------
;Icons

    !define MUI_ICON "..\publish\Resources\openshock-icon.ico"
    !define MUI_UNICON "..\publish\Resources\openshock-icon.ico"

;--------------------------------
;Pages

    !define MUI_PAGE_CUSTOMFUNCTION_PRE SkipIfUpgrade
    !insertmacro MUI_PAGE_LICENSE "..\LICENSE"

    !define MUI_PAGE_CUSTOMFUNCTION_PRE SkipIfUpgrade
    !insertmacro MUI_PAGE_DIRECTORY

    !insertmacro MUI_PAGE_INSTFILES

    ;------------------------------
    ; Finish Page

    ; Checkbox to launch ShockOSC.
    !define MUI_FINISHPAGE_RUN
    !define MUI_FINISHPAGE_RUN_TEXT "Launch ShockOSC"
    !define MUI_FINISHPAGE_RUN_FUNCTION launchShockOSC

    ; Checkbox to create desktop shortcut.
    !define MUI_FINISHPAGE_SHOWREADME
    !define MUI_FINISHPAGE_SHOWREADME_TEXT "Create desktop shortcut"
    !define MUI_FINISHPAGE_SHOWREADME_FUNCTION createDesktopShortcut

    !define MUI_PAGE_CUSTOMFUNCTION_PRE SkipIfUpgrade
    !insertmacro MUI_PAGE_FINISH

    !insertmacro MUI_UNPAGE_CONFIRM
    !insertmacro MUI_UNPAGE_INSTFILES
    !insertmacro MUI_UNPAGE_FINISH

;--------------------------------
;Languages

    !insertmacro MUI_LANGUAGE "English"

;--------------------------------
;Macros

;--------------------------------
;Functions

Function SkipIfUpgrade
    StrCmp $upgradeInstallation 0 noUpgrade
        Abort
    noUpgrade:
FunctionEnd

Function .onInit
    StrCpy $upgradeInstallation 0

    ReadRegStr $R0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShockOSC" "UninstallString"
    StrCmp $R0 "" notInstalled
        StrCpy $upgradeInstallation 1
    notInstalled:

    ; If ShockOSC is already running, display a warning message
    loop:
    StrCpy $1 "OpenShock.ShockOsc.exe"
    nsProcess::_FindProcess "$1"
    Pop $R1
    ${If} $R1 = 0
        MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "ShockOSC is still running. $\n$\nClick `OK` to kill the running process or `Cancel` to cancel this installer." /SD IDOK IDCANCEL cancel
            nsExec::ExecToStack "taskkill /IM OpenShock.ShockOsc.exe"
    ${Else}
        Goto done
    ${EndIf}
    Sleep 1000
    Goto loop

    cancel:
        Abort
    done:
FunctionEnd

Function .onInstSuccess
    ${If} $upgradeInstallation = 1
        Call launchShockOSC
    ${EndIf}
FunctionEnd

Function createDesktopShortcut
    CreateShortcut "$DESKTOP\ShockOSC.lnk" "$INSTDIR\OpenShock.ShockOsc.exe"
FunctionEnd

Function launchShockOSC
    SetOutPath $INSTDIR
    ShellExecAsUser::ShellExecAsUser "" "$INSTDIR\OpenShock.ShockOsc.exe" ""
FunctionEnd

;--------------------------------
;Installer Sections

Section "Install" SecInstall

    StrCmp $upgradeInstallation 0 noUpgrade
        DetailPrint "Uninstall previous version..."
        ExecWait '"$INSTDIR\Uninstall.exe" /S _?=$INSTDIR'
        Delete $INSTDIR\Uninstall.exe
        Goto afterUpgrade
    noUpgrade:
    
    inetc::get "https://aka.ms/vs/17/release/vc_redist.x64.exe" $TEMP\vcredist_x64.exe
    ExecWait "$TEMP\vcredist_x64.exe /install /quiet /norestart"
    Delete "$TEMP\vcredist_x64.exe"

    afterUpgrade:

    SetOutPath "$INSTDIR"

    File /r /x *.log /x *.pdb /x *.mui "..\publish\*.*"

    WriteRegStr HKLM "Software\ShockOSC" "InstallDir" $INSTDIR
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShockOSC" "DisplayName" "ShockOSC"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShockOSC" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShockOSC" "DisplayIcon" "$\"$INSTDIR\Resources\openshock-icon.ico$\""

    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD HKLM  "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShockOSC" "EstimatedSize" "$0"

    CreateShortCut "$SMPROGRAMS\ShockOSC.lnk" "$INSTDIR\OpenShock.ShockOsc.exe"
    ApplicationID::Set "$SMPROGRAMS\ShockOSC.lnk" "ShockOSC"

    WriteRegStr HKCU "Software\Classes\ShockOSC" "" "URL:ShockOSC"
    WriteRegStr HKCU "Software\Classes\ShockOSC" "FriendlyTypeName" "ShockOSC"
    WriteRegStr HKCU "Software\Classes\ShockOSC" "URL Protocol" ""
    WriteRegExpandStr HKCU "Software\Classes\ShockOSC\DefaultIcon" "" "$INSTDIR\Resources\openshock-icon.ico"
    WriteRegStr HKCU "Software\Classes\ShockOSC\shell" "" "open"
    WriteRegStr HKCU "Software\Classes\ShockOSC\shell\open" "FriendlyAppName" "ShockOSC"
    WriteRegStr HKCU "Software\Classes\ShockOSC\shell\open\command" "" '"$INSTDIR\OpenShock.ShockOsc.exe" --uri="%1"'
SectionEnd

;--------------------------------
;Uninstaller Section

Section "Uninstall"
    ; If ShockOSC is already running, display a warning message and exit
    StrCpy $1 "OpenShock.ShockOsc.exe"
    nsProcess::_FindProcess "$1"
    Pop $R1
    ${If} $R1 = 0
        MessageBox MB_OK|MB_ICONEXCLAMATION "ShockOSC is still running. Cannot uninstall this software.$\nPlease close ShockOSC and try again." /SD IDOK
        Abort
    ${EndIf}

    RMDir /r "$INSTDIR"

    DeleteRegKey HKLM "Software\ShockOSC"
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShockOSC"
    DeleteRegKey HKCU "Software\Classes\ShockOSC"

    ${IfNot} ${Silent}
        Delete "$SMPROGRAMS\ShockOSC.lnk"
        Delete "$DESKTOP\ShockOSC.lnk"
    ${EndIf}
SectionEnd
