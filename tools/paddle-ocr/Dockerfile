FROM python:3.10-slim

ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PADDLE_PDX_CACHE_HOME=/opt/paddle-models \
    PADDLE_PDX_MODEL_SOURCE=BOS

RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 libglib2.0-0 libgomp1 libsm6 libxext6 libxrender1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .
RUN pip install -r requirements.txt

COPY app.py .

# 构建时就把 PP-OCRv4 模型下载进镜像，运行时不依赖外网。
# 如果模型下载失败，镜像构建直接失败，避免部署后才发现不能识别。
RUN python -c "import app; print('PaddleOCR models baked into image')"

EXPOSE 5099

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=5 \
  CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:5099/health')"

CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "5099"]
