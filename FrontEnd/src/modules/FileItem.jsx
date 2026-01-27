import './FileItem.scss'
import reactLogo from '../assets/react.svg'
import { newFile } from '../services/filesystem';

export default function FileItem({name, color, scale, path, setPath, selectable, rename}){
   
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
        <div className={`fineder-item scale-${scale}`} onClick={!rename ? ()=>setPath(`${path}${name}/`) : ''}>
            <label className={`selector ${!selectable ? 'd-none' : ''}`}>
                <input type="checkbox" />
                <span class="checkmark"></span>
            </label>
            <div className='fineder-item-content'>
                <div className='finder-icon'>
                    <img src={reactLogo} alt="" />
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