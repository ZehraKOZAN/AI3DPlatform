FROM python:3.11-slim

WORKDIR /app

# trimesh needs a couple of system libs for mesh IO
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

ENV AI_OUTPUT_DIR=/data/output
ENV AI_PUBLIC_BASE_URL=http://localhost:8000/output
VOLUME ["/data/output"]

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
