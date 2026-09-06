# Refuses a deploy that Steam would reject, or that would ship a missing translation.
# Wired into the Deploy target; run it by hand with: powershell -File Tools\preflight.ps1

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path

# Steamworks limits: k_cchPublishedDocumentTitleMax, k_cchPublishedDocumentDescriptionMax, and the
# 1 MB cap on SetItemPreview. RimWorld passes all three through untruncated, so going over fails the
# upload with nothing but "OnItemSubmitted failure. Result: InvalidParam".
$maxTitle = 128
$maxDescription = 8000
$maxPreview = 1048576

$errors = New-Object System.Collections.ArrayList
$warnings = New-Object System.Collections.ArrayList
function Fail([string]$m) { [void]$errors.Add($m) }
function Warn([string]$m) { [void]$warnings.Add($m) }

# --- About.xml -----------------------------------------------------------------------------------

$aboutPath = Join-Path $root 'About\About.xml'
if (-not (Test-Path $aboutPath)) {
    Fail "About\About.xml is missing."
} else {
    $about = [xml](Get-Content $aboutPath -Raw)
    $meta = $about.ModMetaData

    foreach ($field in 'packageId', 'name', 'author', 'modVersion') {
        $value = $meta.$field
        if ([string]::IsNullOrWhiteSpace($value)) {
            Fail "About.xml: <$field> is empty. Without <modVersion> the mod list reads 'unknown'."
        }
    }

    $titleBytes = [System.Text.Encoding]::UTF8.GetByteCount([string]$meta.name)
    if ($titleBytes -gt $maxTitle) {
        Fail ("About.xml: <name> is {0} UTF-8 bytes, Steam allows {1}." -f $titleBytes, $maxTitle)
    }

    # Measured on the decoded text with CRLF, in UTF-8 bytes: see the Media check below.
    $description = [string]$meta.description
    $normalized = ($description -replace "`r`n", "`n") -replace "`n", "`r`n"
    $bytes = [System.Text.Encoding]::UTF8.GetByteCount($normalized)
    if ([string]::IsNullOrWhiteSpace($description)) {
        Fail "About.xml: <description> is empty."
    } elseif ($bytes -gt $maxDescription) {
        Fail ("About.xml: <description> is {0} UTF-8 bytes, Steam allows {1}. Cut {2}." -f $bytes, $maxDescription, ($bytes - $maxDescription))
    } else {
        Write-Host ("  About.xml description {0}/{1} bytes" -f $bytes, $maxDescription)
    }

    # Steam builds the item tags from these, so an empty list uploads an untagged item.
    $versions = @($meta.supportedVersions.li)
    if ($versions.Count -eq 0) {
        Fail "About.xml: <supportedVersions> is empty; the Workshop item would carry no version tag."
    }
}

# --- Preview and Workshop id ---------------------------------------------------------------------

$previewPath = Join-Path $root 'About\Preview.png'
if (-not (Test-Path $previewPath)) {
    Fail "About\Preview.png is missing; RimWorld warns and uploads the item with no image."
} else {
    $size = (Get-Item $previewPath).Length
    if ($size -gt $maxPreview) {
        Fail ("About\Preview.png is {0:N0} bytes, Steam allows {1:N0}." -f $size, $maxPreview)
    }
}

$idPath = Join-Path $root 'About\PublishedFileId.txt'
if (-not (Test-Path $idPath)) {
    Warn "About\PublishedFileId.txt absent: the next upload creates a NEW Workshop item. Expected only for a first release."
} else {
    $id = (Get-Content $idPath -Raw).Trim()
    if ($id -notmatch '^\d+$') {
        Fail "About\PublishedFileId.txt does not hold a numeric id ('$id'); the upload would fail."
    } else {
        Write-Host "  workshop item $id"
    }
}

# --- Workshop descriptions -----------------------------------------------------------------------
# The BBCode versions pasted into the Workshop page by hand. Same 8000 character cap, and the web
# form reports going over as nothing but "a problem occurred while saving the title and description".

foreach ($name in 'workshop-description-en.txt', 'workshop-description-fr.txt') {
    $path = Join-Path $root "Media\$name"
    if (-not (Test-Path $path)) {
        Warn "Media\$name is missing."
        continue
    }
    # Measured in UTF-8 bytes, which is the binding limit: k_cchPublishedDocumentDescriptionMax counts
    # C chars, so every accent costs two. The English text saved fine at 7652 characters while the
    # French text failed at 7789, which is how this was pinned down.
    $text = [System.IO.File]::ReadAllText($path)
    $normalized = ($text -replace "`r`n", "`n") -replace "`n", "`r`n"
    $bytes = [System.Text.Encoding]::UTF8.GetByteCount($normalized)
    if ($bytes -gt $maxDescription) {
        Fail ("Media\{0} is {1} UTF-8 bytes ({2} chars), Steam allows {3}. Cut {4}." -f $name, $bytes, $normalized.Length, $maxDescription, ($bytes - $maxDescription))
    } else {
        Write-Host ("  {0} {1}/{2} bytes, {3} chars" -f $name, $bytes, $maxDescription, $normalized.Length)
    }
}

$dllPath = Join-Path $root '1.6\Assemblies\SmartAgriculture.dll'
if (-not (Test-Path $dllPath)) {
    Fail "1.6\Assemblies\SmartAgriculture.dll is missing; a download from GitHub would be inert."
}

# --- Keyed translations --------------------------------------------------------------------------
# A pipeline with ForEach-Object silently collects nothing here, so these are plain foreach loops.

function Get-Matches([string]$path, [string]$pattern, [string[]]$include) {
    $found = New-Object System.Collections.Generic.HashSet[string]
    if (-not (Test-Path $path)) { return $found }
    $files = if (Test-Path $path -PathType Leaf) { @(Get-Item $path) } else { Get-ChildItem $path -Recurse -File -Include $include }
    foreach ($file in $files) {
        foreach ($m in [regex]::Matches((Get-Content $file.FullName -Raw), $pattern)) {
            [void]$found.Add($m.Groups[1].Value)
        }
    }
    return $found
}

$used = Get-Matches (Join-Path $root 'Source') '"(SACL\.[A-Za-z0-9_]+)"' @('*.cs')
$en = Get-Matches (Join-Path $root 'Languages\English\Keyed\SACL.xml') '<(SACL\.[A-Za-z0-9_]+)>' @()
$fr = Get-Matches (Join-Path $root 'Languages\French\Keyed\SACL.xml') '<(SACL\.[A-Za-z0-9_]+)>' @()

# Some keys are reached as key + "Bare" (Dialog_FieldPlan.FallowTooltip), so a defined key that never
# appears literally in the source is not necessarily dead. Only the missing direction is an error.
foreach ($key in $used) {
    if (-not $en.Contains($key)) { Fail "Keyed: $key is used in C# but missing from English." }
    if (-not $fr.Contains($key)) { Fail "Keyed: $key is used in C# but missing from French." }
}
foreach ($key in $en) {
    if (-not $fr.Contains($key)) { Fail "Keyed: $key exists in English but not in French." }
}
foreach ($key in $fr) {
    if (-not $en.Contains($key)) { Fail "Keyed: $key exists in French but not in English." }
}
Write-Host ("  keyed {0} used, {1} defined" -f $used.Count, $en.Count)

# --- Def injections ------------------------------------------------------------------------------

$defNames = Get-Matches (Join-Path $root '1.6\Defs') '<defName>([^<]+)</defName>' @('*.xml')
$injectedDir = Join-Path $root 'Languages\French\DefInjected'
$injected = ''
if (Test-Path $injectedDir) {
    foreach ($file in Get-ChildItem $injectedDir -Recurse -File -Include '*.xml') {
        $injected += Get-Content $file.FullName -Raw
    }
}
foreach ($defName in $defNames) {
    if ($injected -notmatch [regex]::Escape("<$defName.label>")) {
        Fail "DefInjected: $defName has no French label."
    }
}
Write-Host ("  defs {0} checked" -f $defNames.Count)

# --- Report --------------------------------------------------------------------------------------

foreach ($w in $warnings) { Write-Host "PREFLIGHT WARNING: $w" -ForegroundColor Yellow }
foreach ($e in $errors) { Write-Host "PREFLIGHT ERROR: $e" -ForegroundColor Red }
if ($errors.Count -gt 0) {
    Write-Host ("Preflight failed with {0} error(s)." -f $errors.Count) -ForegroundColor Red
    exit 1
}
Write-Host "Preflight OK."
exit 0
