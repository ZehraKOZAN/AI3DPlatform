'use client';

import { useEffect, useState } from 'react';
import { api, type Product } from '@/lib/api';
import ProductCard from '@/components/ProductCard';

export default function DashboardPage() {
  const [products, setProducts] = useState<Product[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!localStorage.getItem('token')) {
      window.location.href = '/login';
      return;
    }
    api
      .listProducts()
      .then(setProducts)
      .catch((e) => setError(e.message));
  }, []);

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Ürünler</h1>
          <p>Fotoğraflardan oluşturulan 3D modellerinizi yönetin.</p>
        </div>
      </div>

      {error && <p style={{ color: 'var(--failed)' }}>{error}</p>}

      <div className="product-grid">
        <a className="add-product-tile" href="/products/new">
          + Ürün ekle
        </a>
        {products?.map((p) => (
          <ProductCard key={p.id} product={p} />
        ))}
      </div>
    </>
  );
}
