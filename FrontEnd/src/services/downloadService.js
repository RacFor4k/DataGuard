// src/services/DownloadService.js
import streamSaver from 'streamsaver';

// Опционально: если вы хотите захостить вспомогательные файлы библиотеки у себя (рекомендуется для продакшена)
streamSaver.mitm = '/mitm.html'; 

export class DownloadService {
  /**
   * @param {Object} options
   * @param {string} options.url - API адрес
   * @param {string} options.fileName - Имя файла
   * @param {string} options.token - JWT токен
   * @param {Function} options.onProgress - Колбэк прогресса
   */
  static async download({ url, fileName, token, onProgress }) {
    let worker = null;
    let writer = null;

    try {
    
      // 1. Создаем поток записи через StreamSaver
      // Это сразу инициирует загрузку в браузере (появится файл в загрузках)
      const fileStream = streamSaver.createWriteStream(fileName);
      writer = fileStream.getWriter();

      // 2. Запускаем воркер
      worker = new Worker(
        new URL('../workers/download.worker.js', import.meta.url),
        { type: 'module' }
      );

      return new Promise((resolve, reject) => {
        worker.postMessage({ url, token });

        worker.onmessage = async (e) => {
          const { type, chunk, progress, message } = e.data;

          if (type === 'CHUNK') {
            // Записываем чанк прямо в поток StreamSaver
            // Это "скармливает" байты браузеру для сохранения на диск
            await writer.write(chunk);
            if (onProgress) onProgress(progress);
          } 
          
          else if (type === 'DONE') {
            await writer.close();
            worker.terminate();
            resolve(true);
          } 
          
          else if (type === 'ERROR') {
            writer.abort();
            worker.terminate();
            reject(new Error(message));
          }
        };

        worker.onerror = (err) => {
          if (writer) writer.abort();
          worker.terminate();
          reject(err);
        };
      });

    } catch (err) {
      if (writer) writer.abort();
      if (worker) worker.terminate();
      throw err;
    }
  }
}