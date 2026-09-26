'use client';

import { useEffect, useState } from 'react';
import { api } from '@/lib/api';

export default function NewProductPage() {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!localStorage.getItem('token')) {
      window.location.href = '/login';
    }
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const product = await api.createProduct(name, description || undefined);
      window.location.href = `/products/${product.id}`;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ürün oluşturulamadı.');
      setSaving(false);
    }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Yeni ürün</h1>
          <p>Önce ürünü oluşturun, ardından fotoğraf yükleyebilirsiniz.</p>
        </div>
      </div>

      <form onSubmit={submit} className="panel" style={{ maxWidth: 480 }}>
        <div className="field">
          <label>Ürün adı</label>
          <input value={name} onChange={(e) => setName(e.target.value)} required />
        </div>
        <div className="field">
          <label>Açıklama (opsiyonel)</label>
          <input value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        {error && <p style={{ color: 'var(--failed)', fontSize: 13 }}>{error}</p>}
        <button className="btn-primary" disabled={saving}>
          {saving ? 'Oluşturuluyor…' : 'Ürünü oluştur'}
        </button>
      </form>
    </>
  );
}
