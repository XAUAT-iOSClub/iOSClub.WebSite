# 本地 / 远端 PaddleOCR 服务

财务系统的发票 OCR 独立服务。后端只通过 `Finance__OcrEndpoint` 调用它，不把 PaddleOCR 装进 WebAPI。

## 重要：本地安装不会自动上远端

Git 只提交代码、Dockerfile 和部署脚本。以下内容**不要提交**，也不会自动出现在服务器：

- `.venv/`
- `models/`
- `paddlex-cache/`
- `__pycache__/`
- 本机安装的 `paddlepaddle` / `paddleocr` Python 包
- 本机 Docker 镜像

远端必须重新构建镜像，或者拉取已经构建好的镜像。**只 `git push` 不能保证远端能识别。**

## 本机已验证版本

- Windows 10/11
- Python 3.12
- PaddlePaddle **3.0.0**
- PaddleOCR **3.7.0**
- PP-OCRv4 中文模型
- oneDNN 开启；单张图片约 8 秒，图片型 PDF 约 20–30 秒

> 不要升级到 PaddlePaddle 3.3.1：当前 PaddleOCR/PP-OCRv4 在 oneDNN 下会报
> `ConvertPirAttribute2RuntimeAttribute not support`。3.0.0 实测正常。

## 本机启动

```powershell
cd tools\paddle-ocr
.\.venv\Scripts\activate
pip install -r requirements.txt
.\start-local.ps1
```

或：

```powershell
cd tools\paddle-ocr
.\.venv\Scripts\python.exe -m uvicorn app:app --host 127.0.0.1 --port 5099
```

健康检查：

```powershell
Invoke-RestMethod http://127.0.0.1:5099/health
```

后端配置：

```text
Finance__OcrEndpoint=http://127.0.0.1:5099/ocr
Finance__RecognitionTimeoutSeconds=180
```

## Docker：模型已打进镜像

Dockerfile 在构建阶段就下载并固化 PP-OCRv4 模型到 `/opt/paddle-models`：

- 运行时不再需要下载模型；
- 模型下载失败，镜像构建会直接失败，不会出现“服务起来了但不能识别”；
- 镜像体积较大（PaddlePaddle + 模型，约 1–2 GB）。

本地或服务器构建：

```bash
cd tools/paddle-ocr
docker compose up -d --build
```

使用 GitHub Actions 构建好的镜像：

```bash
export OCR_IMAGE=ghcr.io/<你的GitHub用户名>/finance-paddleocr:latest
docker compose pull
docker compose up -d
```

后端在同一个 Docker 网络时：

```text
Finance__OcrEndpoint=http://finance-ocr:5099/ocr
Finance__RecognitionTimeoutSeconds=180
```

健康检查：

```bash
docker inspect --format '{{.State.Health.Status}}' finance-ocr
# healthy
```

## 部署后必须做一次真实发票 smoke test

PowerShell：

```powershell
cd tools\paddle-ocr
.\smoke-test.ps1 -BaseUrl http://127.0.0.1:5099 `
  -InvoicePath "C:\path\to\your\invoice.png"
```

Linux：

```bash
cd tools/paddle-ocr
./smoke-test.sh http://127.0.0.1:5099 /path/to/invoice.png
```

输出：

```text
OCR OK
text length: 500+
```

如果 smoke test 通过，才能说这台服务器“能识别图片”。只看容器 `healthy` 不够，健康检查只证明服务活着。

## 服务器直接装 Python

要求 Python 3.10–3.12、2 核 4G 以上：

```bash
cd tools/paddle-ocr
python3 -m venv .venv
. .venv/bin/activate
pip install -r requirements.txt
uvicorn app:app --host 127.0.0.1 --port 5099
```

生产环境建议用 systemd 守护。

## 行为

- PDF 先读文本层；有文本且像发票，直接返回，不 OCR；
- 图片型 PDF：PyMuPDF 渲染后逐页 PaddleOCR；
- PNG/JPG/WEBP/BMP：直接 PaddleOCR；
- docx：后端先抽 `word/media` 图片，再交给本服务；
- 返回 `{ "rawText": "..." }`，后端用现有 `ParseText` 解析字段。

## 离线部署

如果服务器不能联网：

1. 在有网机器上 `docker compose build`；
2. `docker save finance-paddleocr:latest -o finance-paddleocr.tar`；
3. 上传后 `docker load -i finance-paddleocr.tar`；
4. `docker compose up -d`（不要加 `--build`）。

镜像里已经有模型，服务器不需要再下载。

## 注意

- OCR 结果仍需在“发票库”里人工校对，尤其是发票号码、金额、销售方。
- 当前服务只监听本机/容器端口；不要直接暴露到公网。
- 远端必须是 x86_64 Linux（或能跑 linux/amd64 容器的 Docker）；ARM 需要单独准备 PaddlePaddle 轮子。
