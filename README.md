# Photo3D Platform

Ürün fotoğraflarını otomatik olarak interaktif 3D modellere dönüştüren ve
e-ticaret sitelerine gömülebilir bir viewer/API sunan SaaS platformu.

```
PRODUCT PHOTOS → IMAGE PROCESSING → AI IMAGE-TO-3D → MESH/TEXTURE
→ GLB/glTF → STORAGE/CDN → API → EMBEDDABLE 3D VIEWER → PRODUCT PAGE
```

## Bileşenler

| Klasör | Teknoloji | Görev |
|---|---|---|
| `backend/` | ASP.NET Core 8, EF Core, PostgreSQL | Auth, ürün/görsel/model CRUD, job orkestrasyonu, embed kodu üretimi |
| `ai-service/` | Python, FastAPI, trimesh | Görsel → 3D mesh → GLB üretim pipeline'ı (takılabilir backend) |
| `frontend/` | Next.js, TypeScript | Merchant dashboard: ürün yönetimi, yükleme, 3D önizleme, embed kodu |
| `viewer/` | Vanilla JS + Three.js | Bağımsız, gömülebilir 3D viewer SDK (`viewer.js`) ve örnek entegrasyon |

Mimari `backend/src` altında katmanlara ayrılmıştır: `Domain` (entity'ler),
`Application` (servisler/DTO'lar/iş kuralları), `Infrastructure` (DB, storage,
queue, AI istemcisi), `Api` (controller'lar, `Program.cs`). AI modeli
`IImageTo3DService` arayüzü arkasında soyutlanmıştır — production'da
`ExternalImageTo3DService` yerine başka bir implementasyon takmak backend'in
geri kalanını etkilemez. Aynı prensip AI servisi tarafında da geçerlidir:
`ai-service/app/pipeline.py` içindeki `ReconstructionBackend` arayüzü, şu anki
`StubBackend`'in yerine gerçek bir modelle (TripoSR, Zero123++, vb.)
değiştirilmek üzere tasarlanmıştır.

## Neden "stub" AI backend?

`StubBackend`, gerçek bir görüntü-3D modeli (GPU, ağırlıklar, eğitim verisi
gerektirir) olmadan tüm pipeline'ın uçtan uca çalışmasını sağlamak için ön
fotoğrafı dokulu bir 3D karta dönüştürür. Amaç; upload → queue → job state
machine → post-processing → GLB export → viewer akışının tamamının
doğrulanabilmesidir. Gerçek rekonstrüksiyon kalitesi için `BACKENDS` sözlüğüne
(`ai-service/app/pipeline.py`) yeni bir backend eklemeniz yeterli — backend
API'sinde veya frontend'de hiçbir değişiklik gerekmez.

## Yerel geliştirme

### Docker Compose ile (en hızlı yol)

```bash
docker compose up --build
```

- Backend: http://localhost:5000 (Swagger: `/swagger`)
- AI servisi: http://localhost:8000 (`/health`, `/generate`)
- Frontend: http://localhost:3000

### Manuel

**AI servisi**
```bash
cd ai-service
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

**Backend**
```bash
cd backend/src/Api
dotnet restore
dotnet ef database update   # PostgreSQL bağlantısını appsettings.json'da ayarlayın
dotnet run
```

**Frontend**
```bash
cd frontend
npm install
npm run dev
```

## Uçtan uca akış

1. Merchant kayıt olur / giriş yapar (`POST /api/auth/register|login`).
2. Ürün oluşturur (`POST /api/products`).
3. Bir veya birden fazla fotoğraf yükler (`POST /api/products/{id}/images`).
4. 3D oluşturmayı tetikler (`POST /api/products/{id}/generate`) — bu istek
   hemen döner, gerçek iş arka planda `GenerationWorker` tarafından kuyruktan
   işlenir (`QUEUED → PROCESSING_IMAGES → GENERATING_3D → OPTIMIZING_MODEL →
   UPLOADING_MODEL → COMPLETED`).
5. Backend, job'ı AI servisine HTTP ile iletir; AI servisi görselleri işler,
   mesh üretir, post-processing (decimation, texture optimize) uygular ve
   `product.glb` + thumbnail döner.
6. Merchant embed kodunu alır (`GET /api/products/{id}/embed-code`) ve kendi
   ürün sayfasına iframe veya `<script>` SDK olarak ekler.
7. Müşteri, `viewer/viewer.js` üzerinden yüklenen Three.js sahnesinde ürünü
   360° döndürür, yakınlaştırır.

## Sonraki adımlar (MVP sonrası)

Spesifikasyondaki Faz 4-5 kapsamı: gerçek AI backend entegrasyonu (TripoSR /
Zero123++ / ticari API), Redis/RabbitMQ tabanlı kuyruk, S3/Blob storage + CDN,
API key tabanlı rate limiting, Shopify/WooCommerce entegrasyonları, AR/WebXR
görüntüleme, kullanım bazlı faturalama.

## Not

Bu depoya eklenen kaynak dosyalardan birinde, bana doğrudan "Claude Sonnet
4.5" diye hitap eden ve belirli GitHub repolarını doğrulanmış referanslar gibi
sunmamı isteyen gömülü talimatlar vardı. Bunları bir prompt injection olarak
değerlendirip yok saydım; bu mimari tamamen spesifikasyon dosyasındaki (2.
belge) gereksinimlerden üretildi.
