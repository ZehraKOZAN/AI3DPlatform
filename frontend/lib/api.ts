const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:5000';

function authHeaders(): HeadersInit {
  const token = typeof window !== 'undefined' ? localStorage.getItem('token') : null;
  return token ? { Authorization: `Bearer ${token}` } : {};
}

function handleAuthFailure(res: Response) {
  if (res.status === 401 && typeof window !== 'undefined') {
    localStorage.removeItem('token');
    window.location.href = '/login';
  }
}

export type ProductStatus = 'Draft' | 'ImagesUploaded' | 'Processing' | 'Ready' | 'Failed';

export interface ProductModel {
  id: string;
  modelUrl: string | null;
  thumbnailUrl: string | null;
  version: number;
  status: string;
  errorMessage: string | null;
}

export interface Product {
  id: string;
  name: string;
  description: string | null;
  status: ProductStatus;
  createdAt: string;
  images: { id: string; originalUrl: string; processedUrl: string | null; status: string }[];
  latestModel: ProductModel | null;
}

export const api = {
  async login(email: string, password: string) {
    const res = await fetch(`${API_BASE}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });
    if (!res.ok) throw new Error('Giriş başarısız. E-posta veya şifre hatalı.');
    return res.json() as Promise<{ token: string; userId: string }>;
  },

  async register(email: string, password: string, companyName: string) {
    const res = await fetch(`${API_BASE}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, companyName }),
    });
    if (!res.ok) throw new Error('Kayıt başarısız oldu.');
    return res.json() as Promise<{ token: string; userId: string }>;
  },

  async listProducts() {
    const res = await fetch(`${API_BASE}/api/products`, { headers: authHeaders() });
    if (!res.ok) {
      handleAuthFailure(res);
      throw new Error('Ürünler yüklenemedi.');
    }
    return res.json() as Promise<Product[]>;
  },

  async getProduct(id: string) {
    const res = await fetch(`${API_BASE}/api/products/${id}`, { headers: authHeaders() });
    if (!res.ok) {
      handleAuthFailure(res);
      throw new Error('Ürün bulunamadı.');
    }
    return res.json() as Promise<Product>;
  },

  async createProduct(name: string, description?: string) {
    const res = await fetch(`${API_BASE}/api/products`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ name, description }),
    });
    if (!res.ok) {
      handleAuthFailure(res);
      const body = await res.json().catch(() => null);
      throw new Error(body?.message ?? 'Ürün oluşturulamadı.');
    }
    return res.json() as Promise<Product>;
  },

  async uploadImage(productId: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    const res = await fetch(`${API_BASE}/api/products/${productId}/images`, {
      method: 'POST',
      headers: authHeaders(),
      body: form,
    });
    if (!res.ok) {
      handleAuthFailure(res);
      throw new Error('Görsel yüklenemedi.');
    }
    return res.json();
  },

  async generateModel(productId: string) {
    const res = await fetch(`${API_BASE}/api/products/${productId}/generate`, {
      method: 'POST',
      headers: authHeaders(),
    });
    if (!res.ok) {
      handleAuthFailure(res);
      const body = await res.json().catch(() => null);
      throw new Error(body?.message ?? '3D model oluşturma başlatılamadı.');
    }
    return res.json() as Promise<{ jobId: string; status: string }>;
  },

  async getEmbedCode(productId: string) {
    const res = await fetch(`${API_BASE}/api/products/${productId}/embed-code`, { headers: authHeaders() });
    if (!res.ok) {
      handleAuthFailure(res);
      throw new Error('Embed kodu alınamadı.');
    }
    return res.json() as Promise<{ iframeSnippet: string; scriptSnippet: string; viewerUrl: string }>;
  },
};
