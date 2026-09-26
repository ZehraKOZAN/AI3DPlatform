from dataclasses import dataclass
from PIL import Image


@dataclass
class ProcessedImage:
    """One validated, background-normalized input image ready for reconstruction."""
    image: Image.Image
    view_angle: str  # "front" | "back" | "left" | "right" | "top" | "unknown"
    quality_score: float


@dataclass
class MeshResult:
    """Raw output of the reconstruction stage, before post-processing."""
    vertices: "list"
    faces: "list"
    texture: Image.Image | None
