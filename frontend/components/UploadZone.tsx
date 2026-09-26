'use client';

import { useState, useCallback } from 'react';

export default function UploadZone({ onFiles }: { onFiles: (files: File[]) => void }) {
  const [dragOver, setDragOver] = useState(false);

  const handleDrop = useCallback(
    (e: React.DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      setDragOver(false);
      const files = Array.from(e.dataTransfer.files).filter((f) => f.type.startsWith('image/'));
      if (files.length) onFiles(files);
    },
    [onFiles]
  );

  return (
    <div
      className={`upload-zone${dragOver ? ' dragover' : ''}`}
      onDragOver={(e) => {
        e.preventDefault();
        setDragOver(true);
      }}
      onDragLeave={() => setDragOver(false)}
      onDrop={handleDrop}
      onClick={() => document.getElementById('file-input')?.click()}
    >
      <p>
        Ürün fotoğraflarını buraya sürükleyin veya <strong>seçmek için tıklayın</strong>
      </p>
      <p style={{ fontSize: 12, marginTop: 4 }}>JPG, PNG veya WEBP · birden fazla açı önerilir</p>
      <input
        id="file-input"
        type="file"
        accept="image/jpeg,image/png,image/webp"
        multiple
        hidden
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          if (files.length) onFiles(files);
          e.target.value = '';
        }}
      />
    </div>
  );
}
