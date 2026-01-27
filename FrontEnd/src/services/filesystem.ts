import { authFetch } from './apiClient';

export interface FolderItem {
  path: string;
  name: string;
  tag: string;
}

export interface FinderResponse {
  folders: FolderItem[];
  files: FolderItem[]; // или отдельный тип, если отличается
}

/**
 * Получить содержимое папки
 */
export async function finderUpdate(path: string) {
  // Кодируем путь для URL
  try{
  const encodedPath = encodeURIComponent(path);
  const response = await authFetch(`finder?path=${encodedPath}`);
  
  if (!response.ok) return [response.status, null];
  return [response.status, await response.json()];
  }
  catch{
    return[401,null];
  }
}

/**
 * Создать новую папку
 */
export async function newFolder(path: string, name: string): Promise<number> {
  try {
    const response = await authFetch('finder/new-folder', {
      method: 'POST',
      body: JSON.stringify({
        Path: path,
        Name: name,
      }),
    });

    return response.status;
  } catch (error) {
    console.error('newFolder error:', error);
    return 500;
  }
}

export async function newFile(path: string, name: string): Promise<number> {
  try {
    const response = await authFetch('finder/new-file', {
      method: 'POST',
      body: JSON.stringify({
        Path: path,
        Name: name,
      }),
    });

    return response.status;
  } catch (error) {
    console.error('newFolder error:', error);
    return 500;
  }
}