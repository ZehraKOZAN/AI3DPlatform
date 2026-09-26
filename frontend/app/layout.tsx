import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'Photo3D — Merchant Panel',
  description: 'Ürün fotoğraflarını interaktif 3D modellere dönüştürün.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="tr">
      <body>
        <div className="app-shell">
          <aside className="sidebar">
            <div className="sidebar-mark">
              photo<span>3d</span>
            </div>
            <nav className="sidebar-nav">
              <a href="/">Ürünler</a>
              <a href="/api-keys">API Anahtarları</a>
              <a href="/usage">Kullanım</a>
              <a href="/settings">Ayarlar</a>
            </nav>
          </aside>
          <main className="content">{children}</main>
        </div>
      </body>
    </html>
  );
}
