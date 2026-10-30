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
$languagePattern = '(?m)^[ \\t]*if \\(config\\.UiItem\\.CurrentLanguage\\.IsNullOrEmpty\\(\\)\\)\\r?\\n[ \\t]*\\{\\r?\\n[ \\t]*config\\.UiItem\\.CurrentLanguage = Thread\\.CurrentThread\\.CurrentCulture\\.TwoLetterISOLanguageName\\.Equals\\("zh", StringComparison\\.CurrentCultureIgnoreCase\\)\\r?\\n[ \\t]*\\? Global\\.Languages\\.First\\(\\)\\r?\\n[ \\t]*: Global\\.Languages\\[2\\];\\r?\\n[ \\t]*\\}';
if (-not [regex]::IsMatch($languageText, $languagePattern)) { throw "Language default block not found; refusing to patch." }
$languageText = [regex]::Replace($languageText, $languagePattern, $newLanguage, 1)
Write-Utf8NoBom $languagePath $languageText

# Point the in-app V2rayWatch updater to this repository.
Replace-TextOnce "v2rayN\Directory.Build.props" '<Version>7.25.6</Version>' '<Version>0.1.0</Version>'

# Give the executable and Windows resources V2rayWatch names.
$projectPath = Join-Path $SourceRoot "v2rayN\v2rayN.csproj"
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

# Generate a small V2rayWatch icon.
$iconPath = Join-Path $SourceRoot "v2rayN\v2rayN\Resources\V2rayWatch.ico"
New-Item -ItemType Directory -Force -Path (Split-Path $iconPath) | Out-Null
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(20, 42, 69))
$font = New-Object System.Drawing.Font("Segoe UI", 92, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, 210, 205))
$graphics.DrawString("VW", $font, $brush, 24, 72)
$handle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($handle)
$stream = [System.IO.File]::Create($iconPath)
$icon.Save($stream)
$stream.Dispose()
$icon.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
$font.Dispose()
$brush.Dispose()

Write-Host "V2rayWatch branding applied to upstream v2rayN 7.25.6."
