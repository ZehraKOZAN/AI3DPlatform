"""
Image-to-3D AI microservice.

Kept deliberately independent from the .NET backend: it only speaks HTTP/JSON
(see ExternalImageTo3DService.cs for the caller). This lets the model behind
`/generate` be swapped, A/B tested, or scaled on GPU nodes without touching
the rest of the platform.
"""
from __future__ import annotations

import logging
import os
import uuid

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field

from app.pipeline import ImageTo3DPipeline
from app.config import get_settings

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("ai-service")

app = FastAPI(title="Image-to-3D AI Service", version="0.1.0")

# The generated GLB/thumbnail are fetched directly by the browser (from the
# merchant dashboard on :3000, or the embedded viewer on a merchant's own
# domain), which is a different origin than this service — without CORS the
# browser silently blocks the fetch and the 3D viewer just stays blank.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["GET"],
    allow_headers=["*"],
)

settings = get_settings()
pipeline = ImageTo3DPipeline(settings)

os.makedirs(settings.output_dir, exist_ok=True)
# Generated GLB/thumbnail files are served from here; glb_url/thumbnail_url
# in GenerateResponse point at this mount. Swap for CDN-backed object storage
# URLs in production (see Storage section of the platform spec).
app.mount("/output", StaticFiles(directory=settings.output_dir), name="output")


class GenerateRequest(BaseModel):
    product_id: uuid.UUID
    image_urls: list[str] = Field(min_length=1)


class GenerateResponse(BaseModel):
    glb_url: str
    thumbnail_url: str
    file_size_bytes: int
    polygon_count: int


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "model": settings.model_backend}


@app.post("/generate", response_model=GenerateResponse)
def generate(request: GenerateRequest) -> GenerateResponse:
    """
    Runs the full image(s) -> 3D mesh -> GLB pipeline for one product and
    returns URLs to the generated, web-optimized assets.

    Single vs multi-view mode is selected automatically based on how many
    image_urls are provided (see app/pipeline.py).
    """
    if not request.image_urls:
        raise HTTPException(status_code=400, detail="At least one image_url is required.")

    try:
        result = pipeline.run(product_id=str(request.product_id), image_urls=request.image_urls)
    except Exception as exc:  # pragma: no cover - top-level safety net
        logger.exception("Generation failed for product %s", request.product_id)
        raise HTTPException(status_code=500, detail=str(exc)) from exc

    return GenerateResponse(**result)
