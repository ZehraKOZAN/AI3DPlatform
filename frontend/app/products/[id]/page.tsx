'use client';

import { useEffect, useState, useCallback } from 'react';
import { api, type Product } from '@/lib/api';
import UploadZone from '@/components/UploadZone';
import ModelViewer from '@/components/ModelViewer';

const STATUS_LABEL: Record<string, string> = {
  Draft: 'Taslak',
  ImagesUploaded: 'Görseller yüklendi',
  Processing: 'İşleniyor',
  Ready: 'Hazır',
  Failed: 'Başarısız',
};

export default function ProductDetailPage({ params }: { params: { id: string } }) {
  const [product, setProduct] = useState<Product | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [embed, setEmbed] = useState<{ iframeSnippet: string; scriptSnippet: string } | null>(null);

  const reload = useCallback(() => {
    api.getProduct(params.id).then(setProduct).catch((e) => setError(e.message));
  }, [params.id]);

  useEffect(() => {
    if (!localStorage.getItem('token')) {
      window.location.href = '/login';
      return;
    }
    reload();
  }, [reload]);

  // Poll while a generation job is in flight so the merchant sees status update.
  useEffect(() => {
    if (product?.status !== 'Processing') return;
    const interval = setInterval(reload, 4000);
    return () => clearInterval(interval);
  }, [product?.status, reload]);

  async function handleFiles(files: File[]) {
    setBusy(true);
    setError(null);
    try {
      for (const file of files) {
        await api.uploadImage(params.id, file);
      }
      reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Yükleme başarısız.');
    } finally {
      setBusy(false);
    }
  }

  async function handleGenerate() {
    setBusy(true);
    setError(null);
    try {
      await api.generateModel(params.id);
      reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : '3D oluşturma başlatılamadı.');
    } finally {
      setBusy(false);
    }
  }

  async function loadEmbed() {
    try {
      setEmbed(await api.getEmbedCode(params.id));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Embed kodu alınamadı.');
    }
  }

  if (!product) return error ? <p style={{ color: 'var(--failed)' }}>{error}</p> : <p>Yükleniyor…</p>;

  return (
    <>
      <div className="page-header">
        <div>
          <h1>{product.name}</h1>
          <p>{STATUS_LABEL[product.status] ?? product.status}</p>
        </div>
        <button className="btn-primary" onClick={handleGenerate} disabled={busy || product.images.length === 0}>
          {product.status === 'Ready' ? '3D modeli yeniden oluştur' : '3D model oluştur'}
        </button>
      </div>

      {error && <p style={{ color: 'var(--failed)' }}>{error}</p>}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24 }}>
        <div>
          <h3 style={{ marginBottom: 10 }}>Ürün fotoğrafları</h3>
          <UploadZone onFiles={handleFiles} />
          <div style={{ display: 'flex', gap: 8, marginTop: 12, flexWrap: 'wrap' }}>
            {product.images.map((img) => (
              <img
                key={img.id}
                src={img.originalUrl}
                alt=""
                style={{ width: 72, height: 72, objectFit: 'cover', borderRadius: 4, border: '1px solid var(--line)' }}
              />
            ))}
          </div>
        </div>

        <div>
          <h3 style={{ marginBottom: 10 }}>3D önizleme</h3>
          {product.latestModel?.modelUrl ? (
            <>
              <ModelViewer modelUrl={product.latestModel.modelUrl} />
              <button className="btn-secondary" style={{ marginTop: 12 }} onClick={loadEmbed}>
                Embed kodunu al
              </button>
              {embed && (
                <div style={{ marginTop: 12 }}>
                  <p style={{ fontSize: 12.5, color: 'var(--ink-soft)', marginBottom: 4 }}>iframe</p>
                  <pre className="embed-code">{embed.iframeSnippet}</pre>
                  <p style={{ fontSize: 12.5, color: 'var(--ink-soft)', margin: '10px 0 4px' }}>JavaScript SDK</p>
                  <pre className="embed-code">{embed.scriptSnippet}</pre>
                </div>
              )}
            </>
          ) : (
            <div className="panel" style={{ color: 'var(--ink-soft)' }}>
              {product.status === 'Processing'
                ? '3D model oluşturuluyor…'
                : product.status === 'Failed' && product.latestModel?.errorMessage
                ? <span style={{ color: 'var(--failed)' }}>Hata: {product.latestModel.errorMessage}</span>
                : 'Henüz bir 3D model yok. Fotoğraf yükleyip oluşturmayı başlatın.'}
            </div>
          )}
        </div>
      </div>
    </>
  );
}
