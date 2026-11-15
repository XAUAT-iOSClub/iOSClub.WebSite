from __future__ import annotations

import io
import os
import threading
from pathlib import Path
from typing import List

# PaddleOCR 3.x 的模型缓存/oneDNN 设置必须在 import paddleocr 之前
BASE_DIR = Path(__file__).resolve().parent
os.environ.setdefault("PADDLE_PDX_CACHE_HOME", str(BASE_DIR / "models"))
os.environ.setdefault("PADDLE_PDX_MODEL_SOURCE", "BOS")

import fitz  # PyMuPDF
import numpy as np
from fastapi import FastAPI, File, HTTPException, UploadFile
from PIL import Image
from paddleocr import PaddleOCR

app = FastAPI(title="Finance PaddleOCR", version="1.0")

# CPU + oneDNN 开启；首次启动会下载/加载 PP-OCRv4 模型。
_ocr = PaddleOCR(
    lang="ch",
    ocr_version="PP-OCRv4",
    use_doc_orientation_classify=False,
    use_doc_unwarping=False,
    use_textline_orientation=False,
    enable_mkldnn=True,
)
_ocr_lock = threading.Lock()


def _ocr_image(image: Image.Image) -> str:
    array = np.array(image.convert("RGB"))
    with _ocr_lock:
        result = _ocr.predict(array)

    lines: List[str] = []
    for page in result or []:
        if isinstance(page, dict):
            lines.extend(str(text) for text in (page.get("rec_texts") or []))
            continue

        # 兼容 PaddleOCR 2.x 的 [[box, (text, score)], ...] 结构
        for item in page or []:
            try:
                lines.append(str(item[1][0]))
            except (IndexError, TypeError):
                continue
    return "\n".join(lines)


def _ocr_pdf(data: bytes) -> str:
    doc = fitz.open(stream=data, filetype="pdf")
    try:
        # 先尝试 PDF 文本层，能读出来就不 OCR
        text_parts: List[str] = []
        for page in doc:
            text_parts.append(page.get_text("text"))
        text = "\n".join(text_parts)
        if len(text) >= 60 and ("发票" in text or "号码" in text):
            return text

        pages: List[str] = []
        for page in doc:
            pix = page.get_pixmap(matrix=fitz.Matrix(2, 2), alpha=False)
            image = Image.frombytes("RGB", (pix.width, pix.height), pix.samples)
            pages.append(_ocr_image(image))
        return "\n".join(pages)
    finally:
        doc.close()


@app.get("/health")
def health():
    return {"ok": True, "engine": "paddleocr", "language": "ch", "version": "3.x"}


@app.post("/ocr")
async def ocr(file: UploadFile = File(...)):
    data = await file.read()
    if not data:
        raise HTTPException(status_code=400, detail="empty file")

    name = (file.filename or "").lower()
    content_type = (file.content_type or "").lower()

    try:
        if name.endswith(".pdf") or "pdf" in content_type:
            raw_text = _ocr_pdf(data)
        else:
            image = Image.open(io.BytesIO(data))
            raw_text = _ocr_image(image)
    except Exception as exc:  # noqa: BLE001
        raise HTTPException(status_code=500, detail=f"OCR failed: {exc}") from exc

    return {"rawText": raw_text}
