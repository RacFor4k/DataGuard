import './FileItem.scss'
import { DownloadService } from '../services/downloadService';
import reactLogo from '../assets/react.svg'
import { newFile } from '../services/filesystem';
import { useState } from 'react';
import { updateJwt } from '../services/auth';
import { useNavigate } from 'react-router-dom';
import { ReactSVG } from 'react-svg'

const BASE_URL = import.meta.env.DEV
    ? 'https://localhost:5001/api/'
    : '/api/';
const CHUNK_SIZE = 1024*1024*5; //  5MB

export default function FileItem({name, color, scale, path, setPath, selectable, rename}){
    const [isDownloading, setIsDownloading] = useState(false);
    const [percent, setPercent] = useState(0);
    const navigate = useNavigate();
        const handleDownload = async () => {
        // Добавляем защиту: если мы в режиме переименования, скачивание не запускаем
        if (rename || isDownloading) return;

        if(!updateJwt()){
            navigate('/');
            return;
        }

        setIsDownloading(true); // Включаем индикатор загрузки
        setPercent(0);

        try {
            await DownloadService.download({
                // Используем name и path напрямую из пропсов
                url: `${BASE_URL}finder/download?path=${encodeURIComponent(path)}&fileName=${encodeURIComponent(name)}`,
                fileName: name,
                token: localStorage['token'],
                onProgress: (p) => setPercent(p),
            });
            console.log("Загрузка завершена");
        } catch (err) {
            console.error("Ошибка:", err);
            alert("Не удалось скачать файл");
        } finally {
            setIsDownloading(false); // Выключаем индикатор в любом случае
        }
    }

    let data;
    if(rename){
        data =     <input
      type='text'
      defaultValue={name}
      onKeyDown={(e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            newFile(path,e.target.value).then((state)=>{
                if(state==200){
                    console.log(setPath, path)
                    setPath(path);
                    
                }
            })
        }
      }}
    />
    }
    else{
        data = <p>{name}</p>
    }
    return (
        <div 
            className={`fineder-item scale-${scale} ${isDownloading ? 'loading' : ''}`} 
            
        >
            <label className={`selector ${!selectable ? 'd-none' : ''}`}>
                <input type="checkbox" onClick={(e) => e.stopPropagation()} />
                <span className="checkmark"></span>
            </label>
            <div className='fineder-item-content' onClick={handleDownload}>
                <div className='finder-icon'>
                    <ReactSVG src='file.svg'/>
                </div>
                <div className='file-data'>
                    {data}
                </div>
            </div>
        </div>
    )
}


export function newfileItem({scale,path,newfile}){

    return (
        <div className='fineder-item'>
            <div className='selector'>
                
            </div>
            <div className='icon'>
            <input 
            type='text'
            defaultValue="Новая папка"
            />
            </div>
            <div className='data'>

            </div>
        </div>
    )
}