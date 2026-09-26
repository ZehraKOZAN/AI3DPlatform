'use client';

import { useEffect, useRef, useState } from 'react';

/**
 * Thin dashboard wrapper around the same Three.js embed script merchants
 * use on their own sites (see /viewer/viewer.js), so the preview a merchant
 * sees here matches what their customers will see.
 */
export default function ModelViewer({ modelUrl }: { modelUrl: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      const THREE = await import('three');
      const { GLTFLoader } = await import('three/examples/jsm/loaders/GLTFLoader.js');
      const { OrbitControls } = await import('three/examples/jsm/controls/OrbitControls.js');

      if (cancelled || !ref.current) return;
      const container = ref.current;
      const width = container.clientWidth;
      const height = container.clientHeight;

      const scene = new THREE.Scene();
      const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 100);
      camera.position.set(2, 1.5, 2);

      const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
      renderer.setSize(width, height);
      container.innerHTML = '';
      container.appendChild(renderer.domElement);

      scene.add(new THREE.HemisphereLight(0xffffff, 0x444444, 1.2));
      const light = new THREE.DirectionalLight(0xffffff, 1.5);
      light.position.set(3, 5, 2);
      scene.add(light);

      const controls = new OrbitControls(camera, renderer.domElement);
      controls.enableDamping = true;
      controls.autoRotate = true;

      new GLTFLoader().load(
        modelUrl,
        (gltf) => {
          const box = new THREE.Box3().setFromObject(gltf.scene);
          const size = box.getSize(new THREE.Vector3());
          const center = box.getCenter(new THREE.Vector3());
          const scale = 1.5 / (Math.max(size.x, size.y, size.z) || 1);
          gltf.scene.scale.setScalar(scale);
          gltf.scene.position.sub(center.multiplyScalar(scale));
          scene.add(gltf.scene);
        },
        undefined,
        (err) => {
          console.error('[ModelViewer]', err);
          if (!cancelled) setError('3D model yüklenemedi. Konsolu kontrol edin.');
        }
      );

      (function animate() {
        if (cancelled) return;
        requestAnimationFrame(animate);
        controls.update();
        renderer.render(scene, camera);
      })();
    })();

    return () => {
      cancelled = true;
    };
  }, [modelUrl]);

  return (
    <div style={{ position: 'relative', width: '100%', height: 360 }}>
      <div ref={ref} style={{ width: '100%', height: '100%', borderRadius: 6, background: '#eef0f3' }} />
      {error && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: 'var(--failed)',
            fontSize: 13,
            textAlign: 'center',
            padding: 16,
          }}
        >
          {error}
        </div>
      )}
    </div>
  );
}
