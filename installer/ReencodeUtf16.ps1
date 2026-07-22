param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Dest
)

# schtasks /Create /XML is strict about the file's actual byte encoding
# matching its declared <?xml ... encoding="UTF-16"?>; Inno Setup's Pascal
# Script can only write ANSI/UTF-8 directly, so this re-saves the file as
# real UTF-16 LE with a BOM, which is what schtasks reliably accepts.
$content = Get-Content -Raw -LiteralPath $Source
Set-Content -LiteralPath $Dest -Value $content -Encoding Unicode -NoNewline
