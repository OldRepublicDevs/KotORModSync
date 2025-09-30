$content = Get-Content 'mod-builds\content\k1\full.md' -Raw

# Find "## Mod List" and extract content after it
$modListIndex = $content.IndexOf("## Mod List")
if ($modListIndex -ge 0) {
    $content = $content.Substring($modListIndex)
    Write-Host "Found '## Mod List' at index $modListIndex, content length after: $($content.Length)"
} else {
    Write-Host "ERROR: '## Mod List' not found!"
}

# Test the outer pattern
$pattern = '(?m)^###\s*.+?$[\s\S]*?(?=^___\s*$)'
$m = [regex]::Matches($content, $pattern)
Write-Host "Total m with outer pattern: $($m.Count)"

# Show first 10 match starts
for($i=0; $i -lt [Math]::Min(10, $m.Count); $i++) {
    $preview = $m[$i].Value.Substring(0, [Math]::Min(80, $m[$i].Value.Length)).Replace("`r", "").Replace("`n", " ")
    Write-Host "Match $($i+1): $preview..."
}

