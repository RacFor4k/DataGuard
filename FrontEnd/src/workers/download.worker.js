// src/workers/download.worker.js

self.onmessage = async (e) => {
  const { url, token } = e.data;

  try {
    const response = await fetch(url, {
      headers: { 'Authorization': `Bearer ${token}` }
    });

    if (!response.ok) throw new Error(`Ошибка сервера: ${response.status}`);

    const contentLength = response.headers.get('Content-Length');
    const total = contentLength ? parseInt(contentLength, 10) : 0;
    
    const reader = response.body.getReader();
    let loaded = 0;

    while (true) {
      const { done, value } = await reader.read(); // value - это Uint8Array
      
      if (done) break;

      loaded += value.length;
      const progress = total ? Math.round((loaded / total) * 100) : 0;

      // Передаем чанк в главный поток
      // Используем Transferable (value.buffer), чтобы не копировать данные в памяти
      self.postMessage({ 
        type: 'CHUNK', 
        chunk: value, 
        progress 
      }, [value.buffer]); 
    }

    self.postMessage({ type: 'DONE' });

  } catch (error) {
    self.postMessage({ type: 'ERROR', message: error.message });
  }
};