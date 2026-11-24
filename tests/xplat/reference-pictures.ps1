# Takes the reference pictures of upstream's WinForms app into docs/xplat/reference/windows (PLAN.md section 9, question 14):
# the parity reference on a machine where the original cannot run. Windows only.
#
# Build the WinForms app first (dotnet build GitExtensions.slnx -c Release). The script runs a portable copy of it
# (IsPortable, settings next to the copy) on a demo repository in a temporary folder, so the user's own settings are not
# read or written. Each window is opened alone by one of upstream's command line verbs, found through UI Automation and
# saved with PrintWindow. Upstream's HKCU\Software\GitExtensions key is compared before and after; the app could change
# it (InstallDir) if its startup check were on.
#
# Usage: tests/xplat/reference-pictures.ps1 [-Theme light|dark] [-Scene browse,commit,settings,dialogs]
# Not taken by the script: the output window (a click through UI Automation on a dialog's button does not run its
# command, and a remote operation stops at Pageant when git's SSH client is plink), and the plugins' settings pages (the
# settings verb loads no plugin). Take those by hand and name them as below.
param(
    [ValidateSet('light', 'dark')] [string] $Theme = 'light',
    [string[]] $Scene = @('browse', 'commit', 'settings', 'dialogs')
)
$ErrorActionPreference = 'Stop'
$Scene = @($Scene -split ',')
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$build = Join-Path $repoRoot 'artifacts\Release\bin\GitExtensions\net10.0-windows'
$out = Join-Path $repoRoot 'docs\xplat\reference\windows'
$work = Join-Path ([System.IO.Path]::GetTempPath()) 'xplat-reference'
$app = Join-Path $work 'app'
$repo = Join-Path $work 'demo'
$suffix = if ($Theme -eq 'dark') { '-dark' } else { '' }

if (-not (Test-Path "$build\GitExtensions.exe")) {
    throw "Build the WinForms app first: dotnet build GitExtensions.slnx -c Release"
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class ReferenceCapture
{
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    // Physical pixels whatever the process was started with (a scaled rectangle clips the picture), and
    // PW_RENDERFULLCONTENT (2), which draws the window even where another one covers it.
    public static void Save(IntPtr hwnd, string file)
    {
        SetThreadDpiAwarenessContext(new IntPtr(-4));
        RECT r; GetWindowRect(hwnd, out r);
        using (Bitmap bitmap = new Bitmap(r.Right - r.Left, r.Bottom - r.Top, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2);
                g.ReleaseHdc(hdc);
            }
            bitmap.Save(file, ImageFormat.Png);
        }
    }
}
'@ -ReferencedAssemblies System.Drawing, System.Drawing.Primitives, System.Drawing.Common, System.Private.Windows.GdiPlus, System.Private.Windows.Core

$auto = [System.Windows.Automation.AutomationElement]
$tree = [System.Windows.Automation.TreeScope]
$controls = [System.Windows.Automation.ControlType]

function Export-UpstreamKey([string] $file) {
    & reg.exe export 'HKCU\Software\GitExtensions' $file /y | Out-Null
}

# A portable copy of the build with a fresh settings file: the language, the theme, and no first-start questions.
function Initialize-App {
    Remove-Item -Recurse -Force $app -ErrorAction SilentlyContinue
    robocopy $build $app /E /NFL /NDL /NJH /NJS /NP | Out-Null
    $config = "$app\GitExtensions.dll.config"
    (Get-Content $config -Raw) -replace '(<setting name="IsPortable" serializeAs="String">\s*<value>)False(</value>)', '${1}True${2}' |
        Set-Content $config -NoNewline
    $settings = [ordered]@{
        translation = 'English'
        uitheme_v2 = $Theme
        uithemeisbuiltin_v2 = 'true'
        TelemetryEnabled = 'false'
        CheckForUpdates = 'false'
    }
    $items = foreach ($key in $settings.Keys) {
        "  <item>`r`n    <key>`r`n      <string>$key</string>`r`n    </key>`r`n    <value>`r`n      <string>$($settings[$key])</string>`r`n    </value>`r`n  </item>"
    }
    Set-Content "$app\GitExtensions.settings" ("<?xml version=`"1.0`" encoding=`"utf-8`"?>`r`n<dictionary>`r`n" + ($items -join "`r`n") + "`r`n</dictionary>") -Encoding utf8
}

# The demo repository: a little history with a merged branch, a tag, an open branch and a local origin, then changes in
# all three states, so every list in the windows has something. Dates are fixed, so pictures differ only where the app
# does.
function Initialize-Repository {
    Remove-Item -Recurse -Force $repo, "$work\origin.git" -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $repo | Out-Null
    $names = @{ GIT_AUTHOR_NAME = 'Alex Example'; GIT_AUTHOR_EMAIL = 'alex@example.invalid'
        GIT_COMMITTER_NAME = 'Alex Example'; GIT_COMMITTER_EMAIL = 'alex@example.invalid' }
    foreach ($name in $names.Keys) { Set-Item "Env:$name" $names[$name] }
    Push-Location $repo
    try {
        git init -q -b master
        git config user.name 'Alex Example'; git config user.email 'alex@example.invalid'; git config core.autocrlf false
        function Commit-File([string] $path, [string] $text, [string] $message, [string] $date) {
            New-Item -ItemType Directory -Force (Split-Path -Parent (Join-Path (Get-Location) $path)) | Out-Null
            Set-Content $path $text -NoNewline
            git add -A
            $env:GIT_AUTHOR_DATE = $date; $env:GIT_COMMITTER_DATE = $date
            git commit -q -m $message
        }
        Commit-File 'README.md' "# Demo`n`nA small project for the reference pictures.`n" 'Initial commit' '2026-11-02T10:00:00'
        Commit-File 'src/Parser.cs' "namespace Demo;`n`npublic static class Parser`n{`n    public static int Parse(string text) => int.Parse(text);`n}`n" 'Add the parser' '2026-11-03T11:20:00'
        Commit-File 'README.md' "# Demo`n`nA small project for the reference pictures.`n`nRun it with dotnet run.`n" 'Explain how to run it' '2026-11-04T09:15:00'
        git tag -a v1.0 -m 'First release'
        git switch -q -c feature/login
        Commit-File 'src/Login.cs' "namespace Demo;`n`npublic sealed class Login`n{`n    public bool Check(string user) => user.Length > 0;`n}`n" 'Add the login form' '2026-11-05T14:30:00'
        Commit-File 'src/Login.cs' "namespace Demo;`n`npublic sealed class Login`n{`n    public bool Check(string user) => !string.IsNullOrWhiteSpace(user);`n}`n" 'Refuse blank user names' '2026-11-06T16:45:00'
        git switch -q master
        $env:GIT_AUTHOR_DATE = '2026-11-07T10:00:00'; $env:GIT_COMMITTER_DATE = '2026-11-07T10:00:00'
        git merge -q --no-ff feature/login -m "Merge branch 'feature/login'"
        git switch -q -c feature/export
        Commit-File 'src/Export.cs' "namespace Demo;`n`npublic static class Export`n{`n    public static string ToCsv(int[] values) => string.Join(',', values);`n}`n" 'Export to CSV' '2026-11-09T13:05:00'
        git switch -q master
        Commit-File 'src/Parser.cs' "namespace Demo;`n`npublic static class Parser`n{`n    public static int Parse(string text) => int.Parse(text.Trim());`n}`n" 'Trim the input before parsing' '2026-11-10T08:40:00'
        git clone -q --bare . "$work\origin.git"
        git remote add origin "$work\origin.git"
        git fetch -q origin
        git branch -q -u origin/master master
        Commit-File 'CHANGELOG.md' "## 1.1`n`n- Trimmed input.`n" 'Start the changelog' '2026-11-11T15:00:00'
        Set-Content 'src/Parser.cs' "namespace Demo;`n`npublic static class Parser`n{`n    // Accepts surrounding spaces.`n    public static int Parse(string text) => int.Parse(text.Trim());`n}`n" -NoNewline
        Set-Content 'README.md' "# Demo`n`nA small project for the reference pictures.`n`nRun it with dotnet run, or dotnet test for the tests.`n" -NoNewline
        git add README.md
        Set-Content 'notes.txt' "To do: parse negative numbers.`n" -NoNewline
    }
    finally {
        Pop-Location
        foreach ($name in @($names.Keys) + @('GIT_AUTHOR_DATE', 'GIT_COMMITTER_DATE')) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue }
    }
}

function Start-App([string] $arguments) {
    Start-Process (Join-Path $app 'GitExtensions.exe') -ArgumentList $arguments -WorkingDirectory $repo -PassThru
}

function Stop-App([System.Diagnostics.Process] $process) {
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(5000)) { $process.Kill() }
    }
}

function Get-Windows([System.Diagnostics.Process] $process) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($auto::ProcessIdProperty, $process.Id)
    @($auto::RootElement.FindAll($tree::Children, $condition))
}

function Wait-Window([System.Diagnostics.Process] $process, [scriptblock] $match, [int] $seconds = 60) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $found = Get-Windows $process | Where-Object { & $match $_ } | Select-Object -Last 1
        if ($found) { return $found }
        Start-Sleep -Milliseconds 300
    }
    throw "No window matched in $seconds seconds; open: $((Get-Windows $process | ForEach-Object { $_.Current.Name }) -join ', ')"
}

function Find-All($root, [System.Windows.Automation.ControlType] $type) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($auto::ControlTypeProperty, $type)
    @($root.FindAll($tree::Descendants, $condition))
}

function Select-Item($item) {
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}

function Get-SafeName([string] $text) { ($text -replace '[^A-Za-z0-9]+', ' ').Trim() -replace ' ', '' }

# Every picture on the primary monitor at its scaling, wherever the window opened; the browse window also at one size.
function Save-Window($window, [string] $name, [int] $settleMs = 1500, [int] $width = 0, [int] $height = 0) {
    $transform = $null
    if ($window.TryGetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern, [ref] $transform) -and
        $transform.Current.CanMove) {
        $transform.Move(40, 40)
        if ($width -gt 0 -and $transform.Current.CanResize) { $transform.Resize($width, $height) }
    }
    Start-Sleep -Milliseconds $settleMs
    [ReferenceCapture]::Save([IntPtr]$window.Current.NativeWindowHandle, (Join-Path $out "$name$suffix.png"))
    "saved $name$suffix.png"
}

# A settings tree item's names from the root down, as "GitExtensions-Appearance-Colors".
function Get-TreePath($item) {
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $names = @()
    $current = $item
    while ($current -and $current.Current.ControlType -eq $controls::TreeItem) {
        $names = @(Get-SafeName $current.Current.Name) + $names
        $current = $walker.GetParent($current)
    }
    $names -join '-'
}

function Expand-All($root) {
    for ($round = 0; $round -lt 5; $round++) {
        $expanded = 0
        foreach ($item in (Find-All $root $controls::TreeItem)) {
            $pattern = $null
            if ($item.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref] $pattern) -and
                $pattern.Current.ExpandCollapseState -eq [System.Windows.Automation.ExpandCollapseState]::Collapsed) {
                $pattern.Expand()
                $expanded++
            }
        }
        if ($expanded -eq 0) { return }
        Start-Sleep -Milliseconds 300
    }
}

# Upstream's command line verbs that open one window alone, and the form each shows.
$dialogs = [ordered]@{
    FormClone = 'clone'; FormPush = 'push'; FormPull = 'pull'; FormCreateBranch = 'branch'; FormCheckoutBranch = 'checkout'
    FormMergeBranch = 'merge'; FormRebase = 'rebase'; FormStash = 'stash'; FormCreateTag = 'tag'; FormRemotes = 'remotes'
    FormCleanupRepository = 'cleanup'; FormResetChanges = 'reset'; FormGitIgnore = 'gitignore'
    FormFormatPatch = 'formatpatch'; FormFileHistory = 'filehistory src/Parser.cs'; FormBlame = 'blame src/Parser.cs'
    FormAbout = 'about'
}

New-Item -ItemType Directory -Force $out, $work | Out-Null
Export-UpstreamKey "$work\upstream-key-before.reg"
Initialize-App
Initialize-Repository
try {
    foreach ($s in $Scene) {
        switch ($s) {
            'browse' {
                $p = Start-App "browse `"$repo`""
                try {
                    $w = Wait-Window $p { $_.Current.Name -like '*Git Extensions*' }
                    Start-Sleep -Seconds 4
                    foreach ($tab in (Find-All $w $controls::TabItem)) {
                        Select-Item $tab
                        Save-Window $w "FormBrowse-$(Get-SafeName $tab.Current.Name)" 2000 1800 1100
                    }
                }
                finally { Stop-App $p }
            }
            'commit' {
                $p = Start-App 'commit'
                try { Save-Window (Wait-Window $p { $_.Current.Name -like 'Commit*' }) 'FormCommit' 3000 }
                finally { Stop-App $p }
            }
            'settings' {
                $p = Start-App 'settings'
                try {
                    $w = Wait-Window $p { $_.Current.Name -like '*Settings*' }
                    Start-Sleep -Seconds 2
                    Expand-All $w
                    foreach ($item in (Find-All $w $controls::TreeItem)) {
                        $name = Get-TreePath $item
                        Select-Item $item
                        Save-Window $w "FormSettings-$name"
                    }
                }
                finally { Stop-App $p }
            }
            'dialogs' {
                foreach ($form in $dialogs.Keys) {
                    $p = Start-App $dialogs[$form]
                    try { Save-Window (Wait-Window $p { $_.Current.Name.Length -gt 0 }) $form 3000 }
                    catch { "skipped ${form}: $_" }
                    finally { Stop-App $p }
                }
            }
            default { throw "Unknown scene $s" }
        }
    }
}
finally {
    Export-UpstreamKey "$work\upstream-key-after.reg"
    if ((Get-FileHash "$work\upstream-key-before.reg").Hash -ne (Get-FileHash "$work\upstream-key-after.reg").Hash) {
        Write-Warning "HKCU\Software\GitExtensions changed during the run; the key as it was is in $work\upstream-key-before.reg"
    }
}
