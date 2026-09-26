"""
Implements the pipeline described in the spec, section 7-10:

    image(s) -> preprocessing -> feature extraction -> 3D reconstruction
    (single- or multi-view) -> mesh/texture -> post-processing
    (validate, repair, decimate, optimize textures) -> GLB export

The reconstruction step is behind `ReconstructionBackend`, an abstraction with
a `StubBackend` implementation that runs everywhere with no GPU/model weights
so the service is runnable end-to-end out of the box. Swap in a
`TripoSRBackend` / `Zero123PlusBackend` / external-API backend by implementing
the same interface — nothing else in this file needs to change.
"""
from __future__ import annotations

import io
import logging
import os
from abc import ABC, abstractmethod

import numpy as np
import requests
import trimesh
from PIL import Image, ImageFilter

from app.config import Settings
from app.models import ProcessedImage

logger = logging.getLogger("ai-service.pipeline")


# --------------------------------------------------------------------------
# Stage 1: preprocessing / validation (spec section 6)
# --------------------------------------------------------------------------

def _download_image(url: str) -> Image.Image:
    response = requests.get(url, timeout=30)
    response.raise_for_status()
    return Image.open(io.BytesIO(response.content)).convert("RGBA")


def _flatten_to_rgb(image: Image.Image, background: tuple[int, int, int] = (255, 255, 255)) -> Image.Image:
    """
    Composites a (possibly transparent) image onto a solid background before
    converting to RGB. Plain `.convert("RGB")` on an RGBA image just drops the
    alpha channel — any fully-transparent pixels (common in cut-out product
    photos) keep whatever raw RGB value they happened to have, which is often
    (0,0,0) i.e. black, producing a black silhouette instead of a clean card.
    """
    if image.mode in ("RGBA", "LA") or (image.mode == "P" and "transparency" in image.info):
        rgba = image.convert("RGBA")
        flattened = Image.new("RGB", rgba.size, background)
        flattened.paste(rgba, mask=rgba.split()[-1])
        return flattened
    return image.convert("RGB")


def _blur_score(image: Image.Image) -> float:
    """Cheap blur/quality heuristic: variance of a Laplacian-like edge filter.
    Higher = sharper. Swap for a proper no-reference IQA model in production."""
    gray = np.asarray(image.convert("L").filter(ImageFilter.FIND_EDGES), dtype=np.float32)
    return float(gray.var())


def _guess_view_angle(index: int, total: int) -> str:
    order = ["front", "right", "back", "left", "top"]
    return order[index] if index < len(order) else "unknown"


def preprocess_images(image_urls: list[str], min_resolution: int) -> list[ProcessedImage]:
    processed: list[ProcessedImage] = []

    for i, url in enumerate(image_urls):
        image = _download_image(url)

        if min(image.size) < min_resolution:
            raise ValueError(f"Image {url} is below the minimum resolution of {min_resolution}px.")

        quality = _blur_score(image)
        if quality < 5.0:
            logger.warning("Image %s scored low quality (%.2f) — likely blurry.", url, quality)

        # Background removal / product segmentation would run here in
        # production (e.g. rembg, SAM). Left as a no-op in the stub backend.
        normalized = image.resize((1024, 1024))

        processed.append(ProcessedImage(
            image=normalized,
            view_angle=_guess_view_angle(i, len(image_urls)),
            quality_score=quality,
        ))

    return processed


# --------------------------------------------------------------------------
# Stage 2: 3D reconstruction backend abstraction (spec section 7-8)
# --------------------------------------------------------------------------

class ReconstructionBackend(ABC):
    @abstractmethod
    def reconstruct(self, images: list[ProcessedImage]) -> trimesh.Trimesh:
        """Single-image mode gets one ProcessedImage; multi-image mode gets
        several. Must return a textured trimesh.Trimesh."""
        raise NotImplementedError


class StubBackend(ReconstructionBackend):
    """
    Deterministic placeholder reconstruction: extrudes the front product
    image onto a rounded 3D card and applies it as a texture. Produces a
    real, valid textured mesh so the rest of the pipeline (post-processing,
    GLB export, viewer) can be built and tested without GPU inference or
    model weights.

    Replace with TripoSRBackend / Zero123PlusBackend / an external
    image-to-3D API for production-quality reconstructions — see
    IImageTo3DService.cs on the backend for how this stays swappable.
    """

    def reconstruct(self, images: list[ProcessedImage]) -> trimesh.Trimesh:
        front = _flatten_to_rgb(images[0].image)

        depth = 0.15  # relative to a unit-width card
        box = trimesh.creation.box(extents=[1.0, 1.0, depth])

        uv = np.array([
            [0, 1], [1, 1], [1, 0], [0, 0],  # front face (approx)
            [0, 1], [1, 1], [1, 0], [0, 0],
        ])[: len(box.vertices)]

        material = trimesh.visual.texture.SimpleMaterial(image=front)
        box.visual = trimesh.visual.TextureVisuals(uv=uv, material=material, image=front)

        return box


BACKENDS: dict[str, type[ReconstructionBackend]] = {
    "stub": StubBackend,
    # "triposr": TripoSRBackend,        # TODO: implement when GPU infra is available
    # "zero123plus": Zero123PlusBackend,  # TODO: implement for multi-view mode
}


# --------------------------------------------------------------------------
# Stage 3: post-processing (spec section 9)
# --------------------------------------------------------------------------

def postprocess_mesh(mesh: trimesh.Trimesh, target_max_polygons: int) -> trimesh.Trimesh:
    # Validate + repair
    mesh.remove_duplicate_faces()
    mesh.remove_degenerate_faces()
    mesh.fill_holes()

    # Polygon reduction (only triggers on dense meshes from real backends;
    # the stub's simple box is already far below the target)
    if len(mesh.faces) > target_max_polygons:
        mesh = mesh.simplify_quadric_decimation(target_max_polygons)

    return mesh


def optimize_texture(mesh: trimesh.Trimesh, target_size: int) -> trimesh.Trimesh:
    material = getattr(mesh.visual, "material", None)
    image = getattr(material, "image", None) if material else None

    if image is not None and max(image.size) > target_size:
        image.thumbnail((target_size, target_size))

    return mesh


# --------------------------------------------------------------------------
# Orchestration
# --------------------------------------------------------------------------

class ImageTo3DPipeline:
    def __init__(self, settings: Settings):
        self.settings = settings
        backend_cls = BACKENDS.get(settings.model_backend, StubBackend)
        self.backend = backend_cls()
        os.makedirs(settings.output_dir, exist_ok=True)

    def run(self, product_id: str, image_urls: list[str]) -> dict:
        images = preprocess_images(image_urls, self.settings.min_image_resolution)

        mode = "multi-view" if len(images) > 1 else "single-image"
        logger.info("Running %s reconstruction for product %s (%d image(s))", mode, product_id, len(images))

        mesh = self.backend.reconstruct(images)
        mesh = postprocess_mesh(mesh, self.settings.target_max_polygons)
        mesh = optimize_texture(mesh, self.settings.target_texture_size)

        product_dir = os.path.join(self.settings.output_dir, product_id)
        os.makedirs(product_dir, exist_ok=True)

        glb_path = os.path.join(product_dir, "product.glb")
        mesh.export(glb_path, file_type="glb")

        thumbnail_path = os.path.join(product_dir, "thumbnail.jpg")
        _flatten_to_rgb(images[0].image).resize((512, 512)).save(thumbnail_path, "JPEG", quality=85)

        base = self.settings.public_base_url.rstrip("/")
        return {
            "glb_url": f"{base}/{product_id}/product.glb",
            "thumbnail_url": f"{base}/{product_id}/thumbnail.jpg",
            "file_size_bytes": os.path.getsize(glb_path),
            "polygon_count": len(mesh.faces),
        }
