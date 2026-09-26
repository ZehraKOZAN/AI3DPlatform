import type { Product } from '@/lib/api';

const STATUS_LABEL: Record<string, string> = {
  Draft: 'Taslak',
  ImagesUploaded: 'Görseller yüklendi',
  Processing: 'İşleniyor',
  Ready: 'Hazır',
  Failed: 'Başarısız',
};

const STATUS_CLASS: Record<string, string> = {
  Ready: 'ready',
  Processing: 'processing',
  ImagesUploaded: 'processing',
  Failed: 'failed',
};

export default function ProductCard({ product }: { product: Product }) {
  return (
    <a className="product-card" href={`/products/${product.id}`}>
      <div className="product-thumb">
        {product.latestModel?.thumbnailUrl ? (
          <img src={product.latestModel.thumbnailUrl} alt={product.name} />
        ) : product.images[0] ? (
          <img src={product.images[0].originalUrl} alt={product.name} />
        ) : (
          'Görsel yok'
        )}
      </div>
      <div className="product-meta">
        <h3>{product.name}</h3>
        <div className="status-row">
          <span className={`status-dot ${STATUS_CLASS[product.status] ?? ''}`} />
          {STATUS_LABEL[product.status] ?? product.status}
        </div>
      </div>
    </a>
  );
}
