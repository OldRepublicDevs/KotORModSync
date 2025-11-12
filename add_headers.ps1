$csHeader = @"
// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

"@

$axamlHeader = @"
<!-- Copyright 2021-2025 KOTORModSync -->
<!-- Licensed under the Business Source License 1.1 (BSL 1.1). -->
<!-- See LICENSE.txt file in the project root for full license information. -->

"@

function Add-HeadersToFiles {
    param(
        [string]$fileExtension,
        [string]$header,
        [string]$fileType
    )

    Write-Host "Finding $fileType files missing copyright headers..."

    $files = Get-ChildItem -Path . -Filter *.$fileExtension -Recurse -File |
        Where-Object {
            $_.FullName -notlike '*\obj\*' -and
            $_.FullName -notlike '*\bin\*' -and
            $_.FullName -notlike '*\.history\*'
        } |
        ForEach-Object {
            $content = Get-Content $_.FullName -First 5 -ErrorAction SilentlyContinue | Out-String
            if ($content -notmatch 'Copyright 2021-2025 KOTORModSync') {
                $_
            }
        }

    $fileCount = @($files).Count

    if ($fileCount -eq 0) {
        Write-Host "No $fileType files found missing headers."
        return 0
    }

    Write-Host "Found $fileCount $fileType file(s) missing headers. Adding headers now...`n"

    foreach ($file in $files) {
        if ($file) {
            $relativePath = $file.FullName.Replace((Get-Location).Path + '\', '')
            Write-Host "Adding header to: $relativePath"
            $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
            if ($content) {
                Set-Content -Path $file.FullName -Value ($header + $content) -NoNewline
            }
        }
    }

    Write-Host "Done! Added headers to $fileCount $fileType file(s).`n"
    return $fileCount
}

# Process C# files
$csCount = Add-HeadersToFiles -fileExtension "cs" -header $csHeader -fileType "C#"

# Process AXAML files
$axamlCount = Add-HeadersToFiles -fileExtension "axaml" -header $axamlHeader -fileType "AXAML"

$totalCount = $csCount + $axamlCount

if ($totalCount -eq 0) {
    Write-Host "`nAll files have headers. Nothing to do!"
} else {
    Write-Host "`nTotal: Added headers to $totalCount file(s)."
}
