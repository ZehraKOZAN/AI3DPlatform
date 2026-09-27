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


class TripoSRBackend(ReconstructionBackend):
    """
    Wraps the TripoSR single-image-to-3D model (VAST-AI-Research/TripoSR;
    weights pulled from the Hugging Face Hub as stabilityai/TripoSR). Loaded
    once, onto the GPU, when this backend is selected.

    TripoSR is single-image only. In multi-image mode (several product
    photos), only the first/front image is used — true multi-view fusion
    isn't part of the base model. Good enough for a first real integration;
    revisit if multi-view quality matters more than turnaround time.
    """

    def __init__(self, mesh_resolution: int = 256, chunk_size: int = 8192):
        import torch
        import rembg
        from tsr.system import TSR  # provided by /opt/TripoSR, see Dockerfile

        if not torch.cuda.is_available():
            raise RuntimeError(
                "TripoSR backend requires a CUDA GPU, but torch.cuda.is_available() "
                "is False. Check the ai-service container actually has GPU access "
                "(docker-compose.yml 'deploy.resources.reservations.devices'), or "
                "set AI_MODEL_BACKEND=stub to fall back to the placeholder backend."
            )

        self.device = "cuda:0"
        self.mesh_resolution = mesh_resolution

        logger.info("Loading TripoSR weights from Hugging Face Hub (first run downloads ~1.5GB)...")
        self.model = TSR.from_pretrained(
            "stabilityai/TripoSR",
            config_name="config.yaml",
            weight_name="model.ckpt",
        )
        self.model.renderer.set_chunk_size(chunk_size)
        self.model.to(self.device)
        self.rembg_session = rembg.new_session()
        logger.info("TripoSR model loaded on %s.", self.device)

    def reconstruct(self, images: list[ProcessedImage]) -> trimesh.Trimesh:
        import torch
        from tsr.utils import remove_background, resize_foreground

        # TripoSR expects a cleanly-segmented subject on a neutral background,
        # not the raw product photo — run the same background-removal +
        # recentring step its own reference pipeline (run.py) uses.
        segmented = remove_background(images[0].image.convert("RGBA"), self.rembg_session)
        segmented = resize_foreground(segmented, 0.85)

        arr = np.array(segmented).astype(np.float32) / 255.0
        arr = arr[:, :, :3] * arr[:, :, 3:4] + (1 - arr[:, :, 3:4]) * 0.5
        model_input = Image.fromarray((arr * 255.0).astype(np.uint8))

        with torch.no_grad():
            scene_codes = self.model([model_input], device=self.device)
        mesh = self.model.extract_mesh(scene_codes, resolution=self.mesh_resolution)[0]

        # TripoSR outputs a vertex-colored (untextured) mesh — postprocess_mesh
        # and optimize_texture both handle that fine (the latter just skips
        # when there's no material.image to resize).
        return mesh


BACKENDS: dict[str, type[ReconstructionBackend]] = {
    "stub": StubBackend,
    "triposr": TripoSRBackend,
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
