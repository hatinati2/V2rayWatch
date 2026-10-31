param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot
)

$ErrorActionPreference = "Stop"

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Replace-TextOnce([string]$RelativePath, [string]$Old, [string]$New) {
    $path = Join-Path $SourceRoot $RelativePath
    if (-not (Test-Path $path)) { throw "Expected upstream file not found: $RelativePath" }
    $text = [System.IO.File]::ReadAllText($path)
    if (-not $text.Contains($Old)) { throw "Expected text not found in $RelativePath; upstream source may have changed." }
    Write-Utf8NoBom $path $text.Replace($Old, $New)
}

# Brand the WPF desktop application and isolate its settings/startup entry from v2rayN.
Replace-TextOnce "v2rayN\ServiceLib\Global.cs" 'public const string AppName = "v2rayN";' 'public const string AppName = "V2rayWatch";'
Replace-TextOnce "v2rayN\ServiceLib\Global.cs" 'public const string AutoRunName = "v2rayNAutoRun";' 'public const string AutoRunName = "V2rayWatchAutoRun";'
Replace-TextOnce "v2rayN\ServiceLib\Global.cs" '{ ECoreType.v2rayN, "2dust/v2rayN" },' '{ ECoreType.v2rayN, "hatinati2/V2rayWatch" },'
Replace-TextOnce "v2rayN\ServiceLib\Global.cs" 'public const string PromotionUrl = @"aHR0cHM6Ly85LjIzNDQ1Ni54eXovYWJjLmh0bWw=";' 'public const string PromotionUrl = "";'

# Prefer Persian on Persian Windows installations, otherwise English.
$languagePath = Join-Path $SourceRoot "v2rayN\ServiceLib\Handler\ConfigHandler.cs"
$languageText = [System.IO.File]::ReadAllText($languagePath)
$oldLanguage = @'
        if (config.UiItem.CurrentLanguage.IsNullOrEmpty())
        {
            config.UiItem.CurrentLanguage = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.CurrentCultureIgnoreCase)
                ? Global.Languages.First()
                : Global.Languages[2];
        }
'@
$newLanguage = @'
        if (config.UiItem.CurrentLanguage.IsNullOrEmpty())
        {
            var systemLanguage = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
            config.UiItem.CurrentLanguage = systemLanguage switch
            {
                "zh" => Global.Languages[0],
                "fa" => Global.Languages[3],
                _ => Global.Languages[2]
            };
        }
'@
$languageText = $languageText.Replace("`r`n", "`n")
$oldLanguage = $oldLanguage.Replace("`r`n", "`n")
$newLanguage = $newLanguage.Replace("`r`n", "`n")
if (-not $languageText.Contains($oldLanguage)) { throw "Language default block not found; refusing to patch." }
Write-Utf8NoBom $languagePath $languageText.Replace($oldLanguage, $newLanguage)

# Point the in-app V2rayWatch updater to this repository.
Replace-TextOnce "v2rayN\Directory.Build.props" '<Version>7.25.6</Version>' '<Version>0.2.3</Version>'

# Give the executable and Windows resources V2rayWatch names.
$projectPath = Join-Path $SourceRoot "v2rayN\v2rayN\v2rayN.csproj"
$projectText = [System.IO.File]::ReadAllText($projectPath)
if (-not $projectText.Contains('<OutputType>WinExe</OutputType>')) { throw "WPF project output type marker not found." }
$projectText = $projectText.Replace('<OutputType>WinExe</OutputType>', '<OutputType>WinExe</OutputType>' + [Environment]::NewLine + '    <AssemblyName>V2rayWatch</AssemblyName>')
$projectText = $projectText.Replace('v2rayN.ico', 'V2rayWatch.ico')
Write-Utf8NoBom $projectPath $projectText

Replace-TextOnce "v2rayN\v2rayN\Views\MainWindow.xaml" 'Title="v2rayN"' 'Title="V2rayWatch"'
Replace-TextOnce "v2rayN\v2rayN\Views\MainWindow.xaml" 'Icon="/Resources/v2rayN.ico"' 'Icon="/Resources/V2rayWatch.ico"'
Replace-TextOnce "v2rayN\v2rayN\Views\StatusBarView.xaml" 'IconSource="/Resources/v2rayN.ico"' 'IconSource="/Resources/V2rayWatch.ico"'
Replace-TextOnce "v2rayN\v2rayN\Views\StatusBarView.xaml" 'ToolTipText="v2rayN"' 'ToolTipText="V2rayWatch"'

# Remove the upstream promotional menu and its click handler.
$mainXamlPath = Join-Path $SourceRoot "v2rayN\v2rayN\Views\MainWindow.xaml"
$mainXaml = [System.IO.File]::ReadAllText($mainXamlPath)
$promotionIndex = $mainXaml.IndexOf('x:Name="menuPromotion"')
if ($promotionIndex -lt 0) { throw "Promotion menu not found; refusing to keep unknown upstream link." }
$promotionStart = $mainXaml.LastIndexOf("<MenuItem", $promotionIndex)
$promotionEnd = $mainXaml.IndexOf("</MenuItem>", $promotionIndex)
if ($promotionStart -lt 0 -or $promotionEnd -lt 0) { throw "Could not locate promotion menu block." }
$promotionEnd += "</MenuItem>".Length
$mainXaml = $mainXaml.Remove($promotionStart, $promotionEnd - $promotionStart)
Write-Utf8NoBom $mainXamlPath $mainXaml

$mainCsPath = Join-Path $SourceRoot "v2rayN\v2rayN\Views\MainWindow.xaml.cs"
$mainCs = [System.IO.File]::ReadAllText($mainCsPath)
$mainCs = [regex]::Replace($mainCs, '(?m)^\s*menuPromotion\.Click \+= MenuPromotion_Click;\r?\n', '')
$methodStart = $mainCs.IndexOf('    private void MenuPromotion_Click')
if ($methodStart -ge 0) {
    $methodEnd = $mainCs.IndexOf('    private void MenuSettingsSetUWP_Click', $methodStart)
    if ($methodEnd -lt 0) { throw "Could not find end marker for upstream promotion handler." }
    $mainCs = $mainCs.Remove($methodStart, $methodEnd - $methodStart)
}
Write-Utf8NoBom $mainCsPath $mainCs

# Give the process a distinct Windows taskbar identity before any WPF window is created.
$appCodePath = Join-Path $SourceRoot "v2rayN\v2rayN\App.xaml.cs"
if (-not (Test-Path $appCodePath)) { throw "App.xaml.cs not found; cannot set the Windows taskbar identity." }
$appCode = [System.IO.File]::ReadAllText($appCodePath)
if (-not $appCode.Contains("VwSetCurrentProcessExplicitAppUserModelID")) {
    $fieldMarker = "    public static EventWaitHandle ProgramStarted;"
    if (-not $appCode.Contains($fieldMarker)) { throw "Could not locate App class fields for the taskbar identity." }
    $appIdField = @'
    [global::System.Runtime.InteropServices.DllImport("shell32.dll", EntryPoint = "SetCurrentProcessExplicitAppUserModelID", CharSet = global::System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int VwSetCurrentProcessExplicitAppUserModelID(string appID);
'@
    $appCode = $appCode.Replace($fieldMarker, $appIdField + $fieldMarker)

    $startupMarker = "    protected override void OnStartup(StartupEventArgs e)`r`n    {"
    if (-not $appCode.Contains($startupMarker)) { $startupMarker = "    protected override void OnStartup(StartupEventArgs e)`n    {" }
    if (-not $appCode.Contains($startupMarker)) { throw "Could not locate App.OnStartup for the taskbar identity call." }
    $startupCall = '        try { VwSetCurrentProcessExplicitAppUserModelID("V2rayWatch.Windows.Desktop"); } catch { }'
    $appCode = $appCode.Replace($startupMarker, $startupMarker + [Environment]::NewLine + $startupCall)
    Write-Utf8NoBom $appCodePath $appCode
}
# Generate a distinct multi-resolution V2rayWatch stopwatch icon for the EXE, window and tray.
$iconPath = Join-Path $SourceRoot "v2rayN\v2rayN\Resources\V2rayWatch.ico"
New-Item -ItemType Directory -Force -Path (Split-Path $iconPath) | Out-Null
Add-Type -AssemblyName System.Drawing

$iconImages = New-Object System.Collections.Generic.List[object]
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $scale = $size / 256.0

    $background = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 13, 32, 54))
    $graphics.FillEllipse($background, [single](22 * $scale), [single](22 * $scale), [single](212 * $scale), [single](212 * $scale))

    $outerPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 56, 214, 204), [single](15 * $scale))
    $outerPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $outerPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawEllipse($outerPen, [single](57 * $scale), [single](57 * $scale), [single](142 * $scale), [single](142 * $scale))

    $tickPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 221, 237, 247), [single](8 * $scale))
    $tickPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $tickPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($tickPen, [single](128 * $scale), [single](73 * $scale), [single](128 * $scale), [single](88 * $scale))
    $graphics.DrawLine($tickPen, [single](170 * $scale), [single](128 * $scale), [single](184 * $scale), [single](128 * $scale))
    $graphics.DrawLine($tickPen, [single](128 * $scale), [single](168 * $scale), [single](128 * $scale), [single](183 * $scale))
    $graphics.DrawLine($tickPen, [single](72 * $scale), [single](128 * $scale), [single](86 * $scale), [single](128 * $scale))

    $handPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 246, 250, 255), [single](13 * $scale))
    $handPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($handPen, [single](128 * $scale), [single](128 * $scale), [single](128 * $scale), [single](91 * $scale))
    $graphics.DrawLine($handPen, [single](128 * $scale), [single](128 * $scale), [single](158 * $scale), [single](145 * $scale))

    $centerBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 130, 231, 156))
    $graphics.FillEllipse($centerBrush, [single](116 * $scale), [single](116 * $scale), [single](24 * $scale), [single](24 * $scale))

    $crownPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 130, 231, 156), [single](13 * $scale))
    $crownPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $crownPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($crownPen, [single](128 * $scale), [single](23 * $scale), [single](128 * $scale), [single](43 * $scale))

    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $iconImages.Add([pscustomobject]@{ Size = $size; Bytes = $memory.ToArray() })

    $memory.Dispose()
    $background.Dispose()
    $outerPen.Dispose()
    $tickPen.Dispose()
    $handPen.Dispose()
    $centerBrush.Dispose()
    $crownPen.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$iconStream = [System.IO.File]::Create($iconPath)
$iconWriter = New-Object System.IO.BinaryWriter($iconStream)
try {
    $iconWriter.Write([UInt16]0)
    $iconWriter.Write([UInt16]1)
    $iconWriter.Write([UInt16]$iconImages.Count)
    $imageOffset = 6 + (16 * $iconImages.Count)
    foreach ($entry in $iconImages) {
        $dimension = if ($entry.Size -ge 256) { [byte]0 } else { [byte]$entry.Size }
        $iconWriter.Write($dimension)
        $iconWriter.Write($dimension)
        $iconWriter.Write([byte]0)
        $iconWriter.Write([byte]0)
        $iconWriter.Write([UInt16]1)
        $iconWriter.Write([UInt16]32)
        $iconWriter.Write([UInt32]$entry.Bytes.Length)
        $iconWriter.Write([UInt32]$imageOffset)
        $imageOffset += $entry.Bytes.Length
    }
    foreach ($entry in $iconImages) {
        $iconWriter.Write([byte[]]$entry.Bytes)
    }
}
finally {
    $iconWriter.Dispose()
    $iconStream.Dispose()
}

Write-Host "V2rayWatch branding applied to upstream v2rayN 7.25.6."
