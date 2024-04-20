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

    !insertmacro MUI_PAGE_LICENSE "..\LICENSE"
    !define MUI_PAGE_CUSTOMFUNCTION_PRE dirPre
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

Function dirPre
    StrCmp $upgradeInstallation "true" 0 +2
        Abort
FunctionEnd

Function .onInit
    StrCpy $upgradeInstallation "false"

    ReadRegStr $R0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\ShockOSC" "UninstallString"
    StrCmp $R0 "" done

    ; If ShockOSC is already running, display a warning message
    StrCpy $1 "OpenShock.ShockOsc.exe"
    nsProcess::_FindProcess "$1"
    Pop $R1
    ${If} $R1 = 0
        MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "ShockOSC is still running. $\n$\nClick `OK` to kill the running process or `Cancel` to cancel this installer." /SD IDOK IDCANCEL cancel
            nsExec::ExecToStack "taskkill /IM OpenShock.ShockOsc.exe"
    ${EndIf}

    MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "ShockOSC is already installed. $\n$\nClick `OK` to upgrade the existing installation or `Cancel` to cancel this upgrade." /SD IDOK IDCANCEL cancel
        Goto next
    cancel:
        Abort
    next:
        StrCpy $upgradeInstallation "true"
    done:
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

    StrCmp $upgradeInstallation "true" 0 noupgrade
        DetailPrint "Uninstall previous version..."
        ExecWait '"$INSTDIR\Uninstall.exe" /S _?=$INSTDIR'
        Delete $INSTDIR\Uninstall.exe
        Goto afterupgrade

    noupgrade:

    afterupgrade:

    ReadRegStr $R0 HKLM "SOFTWARE\Classes\Installer\Dependencies\Microsoft.VS.VC_RuntimeMinimumVSU_amd64,v14" "Version"
    IfErrors 0 VSRedistInstalled

    inetc::get "https://aka.ms/vs/17/release/vc_redist.x64.exe" $TEMP\vcredist_x64.exe
    ExecWait "$TEMP\vcredist_x64.exe /install /quiet /norestart"
    Delete "$TEMP\vcredist_x64.exe"
    VSRedistInstalled:

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
    WriteRegStr HKCU "Software\Classes\ShockOSC\shell\open\command" "" '"$INSTDIR\OpenShock.ShockOsc.exe" /uri="%1" /params="%2 %3 %4"'

    ${If} ${Silent}
        SetOutPath $INSTDIR
        ShellExecAsUser::ShellExecAsUser "" "$INSTDIR\OpenShock.ShockOsc.exe" ""
    ${EndIf}

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
