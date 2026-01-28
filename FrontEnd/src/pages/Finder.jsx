import './Finder.scss'
import Header from '../modules/Header';
import SearchBar from '../modules/SearchBar';
import {Toolbar} from '../modules/Toolbar'
import { useEffect, useRef, useState } from 'react';
import {finderUpdate, newFile} from '../services/filesystem'
import FolderItem from '../modules/FolderItem';
import FileItem, { newfileItem } from '../modules/FileItem';
import { AddEvent } from '../services/events';
import { useNavigate } from 'react-router-dom';
import { RandInt } from '../services/crypto';
import { getHashedKey, getToken } from '../services/secretStore';
import { updateJwt } from '../services/auth';

const BASE_URL = import.meta.env.DEV
    ? 'https://localhost:5001/api/'
    : '/api/';
const CHUNK_SIZE = 1024*1024*5; //  5MB



export default function Finder(){
    const [items, setItems] = useState({
  folders: [],
  files: []
});;
    const [scale, setScale] = useState('medium');
    const [path, setPath] = useState('/');
    const [update, Update] = useState(0);
    const [selectable, setSelectable] = useState(false);
    const [newFolder, setNewFolder] = useState(false); 
    const [newFileMenu, setNewFileMenu] = useState(false); 
    const [uploads, setUploads] = useState([])
    const navigate = useNavigate();

    const startUpload = (file) => {
        const id = crypto.randomUUID(); // Генерируем ID для отслеживания

        // Создаем объект загрузки в стейте
        setUploads(prev => [...prev, { id, fileName: file.name, progress: 0 }]);

        // Создаем воркер специально для этого файла
        const worker = new Worker(
        new URL('../workers/upload.worker.js', import.meta.url),
        { type: 'module' }
        );

        worker.onmessage = (event) => {
        const { status, progress, error } = event.data;

        if (status === 'progress') {
            setUploads(prev => prev.map(item => 
            item.id === id ? { ...item, progress } : item
            ));
        } else if (status === 'complete') {
            setUploads(prev => prev.filter(item => item.id !== id));
            AddEvent('info-message', `Файл ${file.name} загружен`, 'success');
            worker.terminate(); // Убиваем воркер после завершения
        } else if (status === 'error') {
            setUploads(prev => prev.filter(item => item.id !== id));
            AddEvent('info-message', `Ошибка: ${file.name}`, 'error');
            worker.terminate();
        }
        };
        const token = getToken();
        if(!updateJwt()){
            navigate('/');
            return;
        }
        const jwtToken = localStorage['token'];
        worker.postMessage({ file, id, url: `${BASE_URL}finder/upload`, chunkSize: CHUNK_SIZE, path, token, jwtToken});
    };

    const handleFileChange = (e) => {
        console.log(e)
        const files = Array.from(e.target.files);
        files.forEach(file => {
            if(!file)
                return;
            console.log(file)
            newFile(path,file.name).then((state)=>{
                if(state==200){
                    startUpload(file);
                    Update(update+1);
                }});
        });
    }

    const NEW_ITEM_MENU = [
    {className:"footer-menu-btn", id:"footer-menu-new-file", text:"Новый файл", action:()=>setNewFileMenu(true)},
    {className:"footer-menu-btn", id:"footer-menu-new-folder", text:"Новая папка", action:()=>setNewFolder(true)},
    {className:"footer-menu-btn", id:"footer-menu-upload-file", text:
        <label>
            <p>Загрузить файл</p>
            <input type='file' className='hide-inp' onChange={handleFileChange}/>
        </label>,
        action: ()=>{},
    }
    ]

    const isRoot = ()=>{
        return path == '/';
    }

    useEffect(()=>{
        finderUpdate(path).then(([state, json])=>{
            setNewFileMenu(false);
            setNewFolder(false);
            if(state==200){
                setItems(json);
            }
            else if(state==401){
                AddEvent('info-message', 'Для доступа необходимо аутентифицироваться!', 'error');
                navigate('/');
            }
        });
        
    },[update, path]);

    useEffect(()=>{
        if(getHashedKey()==null||getToken()==null)
            navigate('/auth/')
    })

    return (
        <div className='finder-content'>
            <div className='finder-head'>
                <div className='finder-head-content'>
                    <Header path={path} setPath={setPath}></Header>
                    {/* <SearchBar></SearchBar> */}
                </div>
                
            </div>
            
            <div className='finder-body'>
                {newFolder && <FolderItem path={path} scale={'medium'} name='Новая папка' rename={true} setPath={(_path=path)=>{setPath(_path); Update(update+1)}}></FolderItem>}
                {newFileMenu && <FileItem path={path} scale={'medium'} name='Новый файл' rename={true} setPath={(_path=path)=>{setPath(_path); Update(update+1)}}></FileItem>}
                <div className='finder-folders'>
                    {console.log(items) || items.folders.map((item)=>(
                        <FolderItem path={path} scale={'medium'} name={item.name} rename={false} setPath={(_path=path)=>{setPath(_path); Update(update+1)}} selectable={selectable}></FolderItem>
                    )) }
                    
                </div>
                <div className='finder-files'>
                    {console.log(items) || items.files.map((item)=>(
                        <FileItem path={path} scale={'medium'} name={item.name} rename={false} setPath={(_path=path)=>{setPath(_path); Update(update+1)}} selectable={selectable}></FileItem>
                    )) }
                </div>
                <div className={`scale-${scale} op-0`}/>
            </div>
            <Toolbar setSelectable={setSelectable} selectable={selectable} menu={NEW_ITEM_MENU}></Toolbar>  
        </div>
    );
}