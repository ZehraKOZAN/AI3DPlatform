FROM pytorch/pytorch:2.1.2-cuda12.1-cudnn8-devel

WORKDIR /app

# Prevent tzdata (pulled in by g++-11/software-properties-common below) from
# blocking the build on an interactive "select your timezone" prompt.
ENV DEBIAN_FRONTEND=noninteractive
ENV TZ=Etc/UTC

# git for cloning TripoSR + torchmcubes, build-essential for compiling the
# torchmcubes CUDA extension, libgl1 for trimesh/opencv-style image IO.
RUN apt-get update && apt-get install -y --no-install-recommends \
    git \
    build-essential \
    software-properties-common \
    libgl1 \
    && add-apt-repository -y ppa:ubuntu-toolchain-r/test \
    && apt-get update \
    && apt-get install -y --no-install-recommends g++-11 \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# torchmcubes has no PyPI release; it's built from source against the CUDA
# toolkit above. If this step fails, TripoSRBackend falls back to raising a
# clear error rather than silently using the stub.
RUN pip install --no-cache-dir scikit-build-core ninja pybind11 cmake
# Restrict to the target GPU's architecture (RTX 40-series = Ada Lovelace,
# sm_89) — without this, the build auto-detects a broad legacy architecture
# list that newer CUDA compilers (12.1 here) refuse to compile for
# (e.g. "nvcc fatal: Unsupported gpu architecture 'compute_35'").
# If you're on a different NVIDIA GPU generation, change this to match:
# Turing (RTX 20xx) = 7.5, Ampere (RTX 30xx) = 8.6, Ada (RTX 40xx) = 8.9.
ENV TORCH_CUDA_ARCH_LIST="8.9"
ENV CC=gcc-11
ENV CXX=g++-11
# CC/CXX alone only affect plain C++ compilation; nvcc picks its own host
# compiler for .cu files unless told otherwise, and defaults back to the
# image's g++-9 (too old for PyTorch's C++17 headers). CUDAHOSTCXX is what
# CMake's CUDA language support actually reads for that.
ENV CUDAHOSTCXX=/usr/bin/g++-11
# torchmcubes' CMakeLists.txt requests C++20, but under C++20 the standard
# library's new std::lerp() clashes with the library's own global lerp()
# helper in cxx/helper_math.h ("conflicts with a previous declaration").
# Cloning it ourselves so we can downgrade it to C++17 (all it actually
# needs) sidesteps that clash entirely.
RUN git clone --depth 1 https://github.com/tatsy/torchmcubes.git /opt/torchmcubes \
    && sed -i -E 's/(CXX_STANDARD[[:space:]]+)20/\117/g; s/(CUDA_STANDARD[[:space:]]+)20/\117/g' /opt/torchmcubes/CMakeLists.txt
RUN pip install --no-cache-dir --no-build-isolation /opt/torchmcubes

# TripoSR itself isn't a pip package — clone it and install its deps, then
# put its `tsr` module on the Python path via a .pth file.
RUN git clone --depth 1 https://github.com/VAST-AI-Research/TripoSR.git /opt/TripoSR \
    && sed -i '/torchmcubes/d' /opt/TripoSR/requirements.txt \
    && pip install --no-cache-dir -r /opt/TripoSR/requirements.txt \
    && echo "/opt/TripoSR" > /opt/conda/lib/python3.10/site-packages/triposr.pth

COPY . .

ENV AI_OUTPUT_DIR=/data/output
ENV AI_PUBLIC_BASE_URL=http://localhost:8000/output
# Model weights (~1.5GB) are downloaded from the Hugging Face Hub on first
# use and cached here; mount a volume over this path to avoid re-downloading
# every time the container is recreated.
ENV HF_HOME=/data/hf-cache
VOLUME ["/data/output", "/data/hf-cache"]

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
