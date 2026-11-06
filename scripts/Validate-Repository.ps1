param([string]$RepositoryRoot = (Get-Location).Path)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path

function Require-File([string]$RelativePath) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing required file: $RelativePath" }
    if ((Get-Item -LiteralPath $path).Length -eq 0) { throw "Required file is empty: $RelativePath" }
}
function Require-One([string]$Label, [string[]]$Names) {
    $found = @(Get-ChildItem -LiteralPath $root -File | Where-Object { $_.Name -in $Names })
    if ($found.Count -ne 1) { throw "Expected exactly one $Label file; found: $($found.Name -join ', ')" }
}
function Require-Link([string]$Document, [string]$Target) {
    $text = Get-Content -LiteralPath (Join-Path $root $Document) -Raw
    if ($text -notmatch [regex]::Escape("($Target)")) { throw "$Document must link to $Target" }
}

@('README.md', 'AGENTS.md', 'CHANGELOG.md', 'CONTRIBUTING.md', 'GLOSSARY.md', 'global.json',
  'docs/repository.md', 'docs/policies/README.md', 'docs/policies/development.md', 'docs/policies/documentation.md',
  'docs/policies/repository-maintenance.md', 'docs/policies/build.md', 'docs/policies/branching.md',
  'docs/policies/commits.md', 'docs/policies/pull-requests.md', 'docs/policies/changelog.md',
  'docs/policies/releases.md', 'docs/policies/testing.md', 'docs/policies/benchmarks.md',
  'docs/policies/logging.md', 'docs/policies/wiki.md',
  'scripts/Invoke-HeadlessTests.ps1', 'scripts/Update-Changelog.ps1', 'scripts/Validate-Repository.ps1', 'scripts/Verify-Release.ps1') | ForEach-Object { Require-File $_ }

# Every policy except the index declares when it must be read; the policy index requires it.
$policyDirectory = Join-Path $root 'docs/policies'
$missingReadWhen = @(Get-ChildItem -LiteralPath $policyDirectory -File -Filter '*.md' |
    Where-Object { $_.Name -ne 'README.md' } |
    Where-Object { -not (Select-String -LiteralPath $_.FullName -Pattern '^Read when:' -Quiet) } |
    ForEach-Object { "docs/policies/$($_.Name)" })
if ($missingReadWhen.Count -gt 0) {
    throw "Policy is missing its 'Read when:' line: $($missingReadWhen -join ', ')"
}
Require-One 'NuGet configuration' @('NuGet.config', 'nuget.config')
Require-One 'license' @('LICENSE', 'LICENSE.md', 'LICENSE.txt')
Require-One 'root solution' @((Get-ChildItem -LiteralPath $root -File |
    Where-Object Extension -in '.sln', '.slnx' | ForEach-Object Name))

# --- Navigation links ------------------------------------------------------------------------
# The policy index is the single list of policies; the entry points route to it, and the
# development policy links every policy.
Require-Link 'AGENTS.md' 'docs/policies/README.md'
Require-Link 'AGENTS.md' 'docs/policies/development.md'
Require-Link 'AGENTS.md' 'docs/repository.md'
Require-Link 'README.md' 'docs/policies/README.md'
Require-Link 'README.md' 'CONTRIBUTING.md'
Require-Link 'README.md' 'docs/repository.md'
Require-Link 'CONTRIBUTING.md' 'docs/policies/README.md'
Require-Link 'CONTRIBUTING.md' 'docs/policies/development.md'
Require-Link 'CONTRIBUTING.md' 'docs/repository.md'
Require-Link 'CONTRIBUTING.md' 'CHANGELOG.md'
Require-Link 'docs/policies/README.md' 'development.md'
Require-Link 'docs/policies/development.md' '../repository.md'
Require-Link 'docs/repository.md' 'policies/development.md'

foreach ($policy in @('documentation.md', 'repository-maintenance.md', 'build.md', 'branching.md',
        'commits.md', 'pull-requests.md', 'changelog.md', 'releases.md', 'testing.md',
        'benchmarks.md', 'logging.md', 'wiki.md')) {
    Require-Link 'docs/policies/development.md' $policy
}

$managed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$rootNames = @('.gitignore', '.gitattributes', '.editorconfig', '.globalconfig', 'global.json',
    'nuget.config', 'gitversion.yml', 'gitversion.yaml', 'license', 'license.md', 'license.txt')
Get-ChildItem -LiteralPath $root -File | Where-Object {
    $_.Name.ToLowerInvariant() -in $rootNames -or
    $_.Extension.ToLowerInvariant() -in @('.md', '.props', '.targets', '.ps1', '.cmd', '.bat', '.sh', '.ruleset', '.runsettings')
} | ForEach-Object { [void]$managed.Add($_.Name) }
foreach ($folder in @('docs', '.github', 'scripts')) {
    $folderPath = Join-Path $root $folder
    if (Test-Path -LiteralPath $folderPath -PathType Container) {
        Get-ChildItem -LiteralPath $folderPath -File -Recurse | Where-Object {
            $_.FullName -notmatch '[\\/](\.git|bin|obj|\.obsidian)[\\/]'
        } | ForEach-Object {
            [void]$managed.Add($_.FullName.Substring($root.Length + 1).Replace('\', '/'))
        }
    }
}

$solution = @(Get-ChildItem -LiteralPath $root -File | Where-Object Extension -in '.sln', '.slnx')[0]
if ($solution.Extension -eq '.slnx') {
    [xml]$xml = Get-Content -LiteralPath $solution.FullName -Raw
    $listed = @($xml.SelectNodes('//File') | ForEach-Object { $_.Path.Replace('\', '/') })
} else {
    $text = Get-Content -LiteralPath $solution.FullName -Raw
    $listed = @([regex]::Matches($text, '(?ms)ProjectSection\(SolutionItems\).*?\n(.*?)\s*EndProjectSection') |
        ForEach-Object { [regex]::Matches($_.Groups[1].Value, '(?m)^\s*(.*?)\s*=') } |
        ForEach-Object { $_.Groups[1].Value.Trim().Replace('\', '/') })
}
$duplicates = @($listed | Group-Object { $_.ToLowerInvariant() } | Where-Object Count -gt 1)
if ($duplicates) { throw "Duplicate solution items: $($duplicates.Name -join ', ')" }
foreach ($item in $listed) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $item) -PathType Leaf)) { throw "Broken solution item: $item" }
}
$missingItems = @($managed | Where-Object { $_ -notin $listed } | Sort-Object)
if ($missingItems) { throw "Files missing from Solution Items: $($missingItems -join ', ')" }

if (Test-Path -LiteralPath (Join-Path $root '.git')) {
    $trackedGenerated = @(& git -C $root ls-files | Where-Object { $_ -match '(^|/)(bin|obj)/' })
    if ($trackedGenerated) { throw "Generated bin/obj files are tracked: $($trackedGenerated -join ', ')" }
}

$resxFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'src'), (Join-Path $root 'tests') -Recurse -File -Filter '*.resx' |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Name -notmatch '\.ru\.resx$' })
foreach ($neutral in $resxFiles) {
    $russianPath = $neutral.FullName -replace '\.resx$', '.ru.resx'
    if (-not (Test-Path -LiteralPath $russianPath -PathType Leaf)) {
        throw "Missing Russian resources for $($neutral.FullName.Substring($root.Length + 1))"
    }
    $neutralText = [System.IO.File]::ReadAllText($neutral.FullName)
    $russianText = [System.IO.File]::ReadAllText($russianPath)
    $neutralKeys = @([regex]::Matches($neutralText, '<data name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    $russianKeys = @([regex]::Matches($russianText, '<data name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    $diff = @(Compare-Object -ReferenceObject $neutralKeys -DifferenceObject $russianKeys -PassThru)
    if ($diff.Count -gt 0) {
        throw "Resource key mismatch in $($neutral.Name)/$([IO.Path]::GetFileName($russianPath)): $($diff -join ', ')"
    }
}

# --- Markdown links and anchors ------------------------------------------------------------
# Every relative link must resolve to an existing file or directory, and a `#anchor` must point at
# a heading that exists in the target document.
function Get-HeadingSlug([string]$Heading) {
    $text = $Heading.Trim().ToLowerInvariant()
    $text = $text -replace '`', ''
    $text = $text -replace '[^\p{L}\p{N}\s\-_]', ''
    $text = $text.Trim()
    $text = $text -replace '\s+', '-'
    return $text
}

function Get-MarkdownSlugs([string]$Path) {
    $slugs = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $counts = @{}
    $inFence = $false
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if ($line -match '^\s*(```|~~~)') {
            $inFence = -not $inFence
            continue
        }
        if ($inFence) { continue }
        if ($line -match '^#{1,6}\s+(?<text>.+?)\s*$') {
            $slug = Get-HeadingSlug $Matches['text']
            if (-not $slug) { continue }
            if ($counts.ContainsKey($slug)) {
                $counts[$slug]++
                $slug = "$slug-$($counts[$slug])"
            } else {
                $counts[$slug] = 0
            }
            [void]$slugs.Add($slug)
        }
    }
    return $slugs
}

$markdownFiles = @(Get-ChildItem -LiteralPath $root -File -Filter '*.md') +
    @(Get-ChildItem -LiteralPath (Join-Path $root 'docs') -Recurse -File -Filter '*.md') +
    @(Get-ChildItem -LiteralPath (Join-Path $root '.github') -Recurse -File -Filter '*.md')
$markdownFiles = @($markdownFiles | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.git|\.obsidian|node_modules)[\\/]' })
$slugCache = @{}
$linkProblems = [Collections.Generic.List[string]]::new()
foreach ($file in $markdownFiles) {
    $relativeFile = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
    $text = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
    $inFence = $false
    foreach ($line in ($text -split '\r?\n')) {
        if ($line -match '^\s*(```|~~~)') { $inFence = -not $inFence; continue }
        if ($inFence) { continue }
        foreach ($match in [regex]::Matches($line, '\]\((?<target>[^)\s]+)(?:\s+"[^"]*")?\)')) {
            $target = $match.Groups['target'].Value
            if ($target -match '^(https?:|mailto:|#!|//)') { continue }
            if ($target -match '^#') {
                $anchor = [Uri]::UnescapeDataString($target.TrimStart('#'))
                if (-not $slugCache.ContainsKey($relativeFile)) { $slugCache[$relativeFile] = Get-MarkdownSlugs $file.FullName }
                if (-not $slugCache[$relativeFile].Contains($anchor)) {
                    $linkProblems.Add("${relativeFile}: anchor '$target' does not match a heading.")
                }
                continue
            }
            $pathPart = $target
            $anchor = $null
            if ($pathPart.Contains('#')) {
                $anchor = [Uri]::UnescapeDataString($pathPart.Substring($pathPart.IndexOf('#') + 1))
                $pathPart = $pathPart.Substring(0, $pathPart.IndexOf('#'))
            }
            $pathPart = [Uri]::UnescapeDataString($pathPart.TrimEnd('/'))
            if (-not $pathPart) { continue }
            $resolved = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $pathPart))
            if (-not ($resolved.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolved))) {
                $linkProblems.Add("${relativeFile}: relative link '$target' does not resolve.")
                continue
            }
            if ($anchor -and (Test-Path -LiteralPath $resolved -PathType Leaf)) {
                $resolvedRelative = $resolved.Substring($root.Length + 1).Replace('\', '/')
                if (-not $slugCache.ContainsKey($resolvedRelative)) { $slugCache[$resolvedRelative] = Get-MarkdownSlugs $resolved }
                if (-not $slugCache[$resolvedRelative].Contains($anchor)) {
                    $linkProblems.Add("${relativeFile}: link '$target' points at a heading that does not exist.")
                }
            }
        }
    }
}
if ($linkProblems.Count -gt 0) {
    throw "Markdown link check failed:`n - $($linkProblems -join "`n - ")"
}

# --- CI configuration ---------------------------------------------------------------------
# Actions are pinned to full release tags (vMAJOR.MINOR.PATCH): a floating major tag is a movable
# pointer, while a full release tag names one release.
$workflowsPath = Join-Path $root '.github/workflows'
if (Test-Path -LiteralPath $workflowsPath -PathType Container) {
    $pinProblems = [Collections.Generic.List[string]]::new()
    foreach ($workflow in @(Get-ChildItem -LiteralPath $workflowsPath -File -Filter '*.yml')) {
        $workflowRelative = $workflow.FullName.Substring($root.Length + 1).Replace('\', '/')
        foreach ($line in [IO.File]::ReadAllLines($workflow.FullName)) {
            if ($line -notmatch '^\s*(?:-\s+)?uses:\s*(?<action>[^\s#]+)') { continue }
            $action = $Matches['action'].Trim('''', '"')
            if ($action.StartsWith('./') -or $action.StartsWith('docker://')) { continue }
            if ($action -notmatch '@v\d+\.\d+\.\d+$') {
                $pinProblems.Add("${workflowRelative}: '$action' is not pinned to a full version tag such as @v7.0.1.")
            }
        }
    }
    if ($pinProblems.Count -gt 0) {
        throw "Workflow action pin check failed:`n - $($pinProblems -join "`n - ")"
    }
}

# --- Encoding -----------------------------------------------------------------------------
# Tracked text files are UTF-8. A broken byte sequence is a defect: the compiler then reads the file
# with a locale-dependent fallback, and tools cannot read it at all.
if (Test-Path -LiteralPath (Join-Path $root '.git')) {
    $textExtensions = @('.md', '.yml', '.yaml', '.ps1', '.json', '.props', '.targets', '.csproj', '.slnx', '.sln',
        '.cs', '.xaml', '.resx', '.config', '.xml', '.txt', '.editorconfig', '.gitignore', '.gitattributes')
    $strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
    $encodingProblems = [Collections.Generic.List[string]]::new()
    foreach ($tracked in @(& git -C $root ls-files)) {
        $extension = [IO.Path]::GetExtension($tracked)
        $name = [IO.Path]::GetFileName($tracked)
        if ($extension -notin $textExtensions -and $name -notin @('.editorconfig', '.gitignore', '.gitattributes')) { continue }
        $trackedPath = Join-Path $root $tracked
        if (-not (Test-Path -LiteralPath $trackedPath -PathType Leaf)) { continue }
        try {
            [void]$strictUtf8.GetString([IO.File]::ReadAllBytes($trackedPath))
        } catch {
            $encodingProblems.Add("${tracked}: not valid UTF-8.")
        }
    }
    if ($encodingProblems.Count -gt 0) {
        throw "Encoding check failed:`n - $($encodingProblems -join "`n - ")"
    }
}

Write-Host "Repository policy validation passed: $($solution.Name); $($listed.Count) solution items; $($markdownFiles.Count) Markdown files checked."
