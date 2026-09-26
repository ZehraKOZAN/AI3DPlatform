'use client';

import { useState } from 'react';
import { api } from '@/lib/api';

export default function LoginPage() {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const result =
        mode === 'login' ? await api.login(email, password) : await api.register(email, password, companyName);
      localStorage.setItem('token', result.token);
      window.location.href = '/';
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Bir hata oluştu.');
    }
  }

  return (
    <div style={{ maxWidth: 360, margin: '80px auto' }}>
      <h1 style={{ marginBottom: 4 }}>{mode === 'login' ? 'Giriş yap' : 'Hesap oluştur'}</h1>
      <p style={{ color: 'var(--ink-soft)', marginBottom: 24 }}>
        Ürün fotoğraflarınızı 3D modellere dönüştürmeye başlayın.
      </p>

      <form onSubmit={submit} className="panel">
        {mode === 'register' && (
          <div className="field">
            <label>Firma adı</label>
            <input value={companyName} onChange={(e) => setCompanyName(e.target.value)} required />
          </div>
        )}
        <div className="field">
          <label>E-posta</label>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </div>
        <div className="field">
          <label>Şifre</label>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} />
        </div>

        {error && <p style={{ color: 'var(--failed)', fontSize: 13 }}>{error}</p>}

        <button type="submit" className="btn-primary" style={{ width: '100%' }}>
          {mode === 'login' ? 'Giriş yap' : 'Hesap oluştur'}
        </button>
      </form>

      <button
        className="btn-secondary"
        style={{ marginTop: 12, width: '100%' }}
        onClick={() => setMode(mode === 'login' ? 'register' : 'login')}
      >
        {mode === 'login' ? 'Hesabın yok mu? Kaydol' : 'Zaten hesabın var mı? Giriş yap'}
      </button>
    </div>
  );
}
