/**
 * Embeddable 3D product viewer SDK.
 *
 * Usage on a merchant's page:
 *
 *   <script src="https://cdn.example.com/viewer.js"></script>
 *   <div class="product-viewer" data-product-id="product_123"></div>
 *
 * The script scans the page for elements with [data-product-id], fetches
 * that product's GLB URL from the platform API, and renders an interactive
 * Three.js viewer with orbit controls, auto-rotate, zoom and a loading state.
 * Merchants never touch Three.js directly.
 */
(function () {
  "use strict";

  const DEFAULT_API_BASE = window.PLATFORM_API_BASE || "https://api.example.com";
  const THREE_CDN = "https://cdnjs.cloudflare.com/ajax/libs/three.js/r160/three.module.min.js";
  const GLTF_LOADER_CDN = "https://cdnjs.cloudflare.com/ajax/libs/three.js/r160/examples/jsm/loaders/GLTFLoader.js";
  const ORBIT_CONTROLS_CDN = "https://cdnjs.cloudflare.com/ajax/libs/three.js/r160/examples/jsm/controls/OrbitControls.js";

  let threeModulePromise = null;
  function loadThree() {
    if (!threeModulePromise) {
      threeModulePromise = Promise.all([
        import(THREE_CDN),
        import(GLTF_LOADER_CDN),
        import(ORBIT_CONTROLS_CDN),
      ]).then(([THREE, { GLTFLoader }, { OrbitControls }]) => ({ THREE, GLTFLoader, OrbitControls }));
    }
    return threeModulePromise;
  }

  async function fetchModelUrl(apiBase, productId) {
    const res = await fetch(`${apiBase}/api/products/${productId}/model`);
    if (!res.ok) throw new Error(`Failed to load product ${productId} (${res.status})`);
    const data = await res.json();
    if (!data.modelUrl) throw new Error(`Product ${productId} has no ready 3D model yet.`);
    return data.modelUrl;
  }

  function buildChrome(container) {
    container.style.position = "relative";
    container.style.width = container.style.width || "100%";
    container.style.height = container.style.height || "500px";
    container.style.background = "#f4f4f5";
    container.style.borderRadius = "8px";
    container.style.overflow = "hidden";

    const loading = document.createElement("div");
    loading.textContent = "Loading 3D model…";
    loading.style.cssText =
      "position:absolute;inset:0;display:flex;align-items:center;justify-content:center;" +
      "font:14px system-ui,sans-serif;color:#71717a;";
    container.appendChild(loading);

    return { loading };
  }

  async function initViewer(container) {
    const productId = container.getAttribute("data-product-id");
    const apiBase = container.getAttribute("data-api-base") || DEFAULT_API_BASE;
    const autoRotate = container.getAttribute("data-auto-rotate") !== "false";

    const { loading } = buildChrome(container);

    try {
      const [{ THREE, GLTFLoader, OrbitControls }, modelUrl] = await Promise.all([
        loadThree(),
        fetchModelUrl(apiBase, productId),
      ]);

      const width = container.clientWidth;
      const height = container.clientHeight;

      const scene = new THREE.Scene();
      const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 100);
      camera.position.set(2, 1.5, 2);

      const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
      renderer.setSize(width, height);
      renderer.outputColorSpace = THREE.SRGBColorSpace;
      container.appendChild(renderer.domElement);

      scene.add(new THREE.HemisphereLight(0xffffff, 0x444444, 1.2));
      const keyLight = new THREE.DirectionalLight(0xffffff, 1.5);
      keyLight.position.set(3, 5, 2);
      scene.add(keyLight);

      const controls = new OrbitControls(camera, renderer.domElement);
      controls.enableDamping = true;
      controls.autoRotate = autoRotate;
      controls.autoRotateSpeed = 2.5;
      controls.minDistance = 0.5;
      controls.maxDistance = 10;

      const loader = new GLTFLoader();
      loader.load(
        modelUrl,
        (gltf) => {
          const model = gltf.scene;

          // Center + normalize scale so every product fills the frame consistently
          const box = new THREE.Box3().setFromObject(model);
          const size = box.getSize(new THREE.Vector3());
          const center = box.getCenter(new THREE.Vector3());
          const maxDim = Math.max(size.x, size.y, size.z) || 1;
          model.scale.setScalar(1.5 / maxDim);
          model.position.sub(center.multiplyScalar(1.5 / maxDim));

          scene.add(model);
          loading.remove();
        },
        undefined,
        (err) => {
          loading.textContent = "Couldn't load this product's 3D model.";
          console.error("[viewer.js]", err);
        }
      );

      function onResize() {
        const w = container.clientWidth;
        const h = container.clientHeight;
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
        renderer.setSize(w, h);
      }
      window.addEventListener("resize", onResize);

      (function animate() {
        requestAnimationFrame(animate);
        controls.update();
        renderer.render(scene, camera);
      })();
    } catch (err) {
      loading.textContent = "Couldn't load 3D viewer.";
      console.error("[viewer.js]", err);
    }
  }

  function initAll() {
    document.querySelectorAll("[data-product-id]").forEach((el) => {
      if (!el.dataset.viewerInitialized) {
        el.dataset.viewerInitialized = "true";
        initViewer(el);
      }
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initAll);
  } else {
    initAll();
  }

  // Exposed for SPA merchants that inject the div dynamically.
  window.PlatformViewer = { init: initAll, initViewer };
})();
