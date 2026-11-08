param(
    [string]$BaseUrl = "http://127.0.0.1:5099",
    [Parameter(Mandatory = $true)]
    [string]$InvoicePath
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $InvoicePath)) {
    throw "发票文件不存在：$InvoicePath"
}

$response = & curl.exe -s -X POST "$BaseUrl/ocr" -F "file=@$InvoicePath"
try {
    $json = $response | ConvertFrom-Json
} catch {
    throw "OCR 服务返回的不是 JSON：$response"
}

if ($json.error) { throw "OCR 服务错误：$($json.error)" }
if ([string]::IsNullOrWhiteSpace($json.rawText) -or $json.rawText.Length -lt 10) {
    throw "OCR 没有返回有效文本"
}

Write-Host "OCR OK"
Write-Host "text length: $($json.rawText.Length)"
Write-Host "--- first 500 chars ---"
Write-Host $json.rawText.Substring(0, [Math]::Min(500, $json.rawText.Length))
