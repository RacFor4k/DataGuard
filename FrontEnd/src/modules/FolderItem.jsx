import './FolderItem.scss'
import { ReactSVG } from 'react-svg';
import { newFolder } from '../services/filesystem';

export default function FolderItem({name, color, scale, path, setPath, selectable, rename}){
   
    let data;
    if(rename){
        data =     <input
      type='text'
      onFocus={(e)=>e.target.select()}
      autoFocus
      defaultValue={name}
      onBlur={(e) => {
            newFolder(path,e.target.value).then((state)=>{
                if(state==200){
                    console.log(setPath, path)
                    setPath(path);
                    
                }
            })
      }}
      onKeyDown={(e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            newFolder(path,e.target.value).then((state)=>{
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
        <div className={`fineder-item scale-${scale}`} >
            <label className={`selector ${!selectable ? 'd-none' : ''}`}>
                <input type="checkbox" />
                <span class="checkmark"></span>
            </label>
            <div className='fineder-item-content' onClick={!rename ? ()=>setPath(`${path}${name}/`) : ''}>
                <div className='finder-icon'>
                    <ReactSVG src='folder.svg'/>
                </div>
                <div className='folder-data'>
                    {data}
                </div>
            </div>
        </div>
    )
}


export function newFolderItem({scale,path,newFolder}){

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