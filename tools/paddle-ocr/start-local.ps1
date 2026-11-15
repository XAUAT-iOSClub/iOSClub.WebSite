param(
    [int]$Port = 5099
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSCommandPath
$python = Join-Path $root ".venv\Scripts\python.exe"
if (-not (Test-Path $python)) {
    throw "找不到虚拟环境：$python。请先按 README 执行 python -m venv .venv 并安装依赖。"
}

$env:PADDLE_PDX_CACHE_HOME = Join-Path $root "paddlex-cache"
$env:PADDLE_PDX_MODEL_SOURCE = "BOS"
Set-Location $root

Write-Host "PaddleOCR: $root"
Write-Host "Listening: http://127.0.0.1:$Port"
& $python -m uvicorn app:app --host 127.0.0.1 --port $Port
