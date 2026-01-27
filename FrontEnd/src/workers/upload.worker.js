self.onmessage = async (e) => {
  const { file, id, url, chunkSize, path, token, jwtToken } = e.data;
  
  try {
    const totalChunks = Math.ceil(file.size / chunkSize);
    
    for (let i = 0; i < totalChunks; i++) {
      const start = i * chunkSize;
      const end = Math.min(start + chunkSize, file.size);
      const chunk = file.slice(start, end);

      const formData = new FormData();
formData.append('Chunk', chunk, file.name); 

// Убедитесь, что Path, FileName и Index соответствуют свойствам в Models.UploadFile
formData.append('Path', path);
formData.append('FileName', file.name);
formData.append('Index', i.toString());

      const response = await fetch(url, { method: 'POST', headers: {'Authorization': `Bearer ${jwtToken}`}, body: formData });
      
      if (!response.ok) throw new Error('Upload failed');

      const progress = Math.round(((i + 1) / totalChunks) * 100);
      self.postMessage({ status: 'progress', id, progress });
    }

    self.postMessage({ status: 'complete', id });
  } catch (error) {
    self.postMessage({ status: 'error', id, error: error.message });
  }
};