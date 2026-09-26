from functools import lru_cache
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # Which underlying image-to-3D backend to use. "stub" ships out of the box
    # so the service runs end-to-end without GPU/model weights; swap in
    # "triposr", "zero123plus", or an external API-backed value in production.
    model_backend: str = "stub"

    output_dir: str = "./output"
    public_base_url: str = "http://localhost:8000/output"

    # Minimum accepted image dimension (spec section 6: resolution
    # validation). Lower this via AI_MIN_IMAGE_RESOLUTION for quick local
    # testing with small/cropped images — keep it at 512+ for real product
    # photos so reconstruction quality isn't fed garbage input.
    min_image_resolution: int = 512

    # Post-processing targets (section 9 of the spec: polygon reduction,
    # texture optimization) — used by app/pipeline.py's optimize step.
    target_max_polygons: int = 50_000
    target_texture_size: int = 2048

    class Config:
        env_prefix = "AI_"


@lru_cache
def get_settings() -> Settings:
    return Settings()
