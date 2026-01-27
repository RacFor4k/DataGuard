import React from "react";
import { Link } from "react-router-dom";
import './Header.scss'
import reactLogo from '../assets/react.svg'


export default function Header({path, setPath}){
    let head = path.split('/').at(-2);
    if(head=='')
        head = 'Обзор'
    return (
        <header className="main-header">
            <div className='content-header'>
                <div className='back-btn' onClick={()=>setPath(path.split('/').slice(0,-2).join('/')+'/')}>
                    <img src={reactLogo} alt="" />
                </div>
                <div className='content-path'>
                    <span>{path.split('/').slice(1).join('/') != '' ? path.split('/').slice(1,-1).join('/') : 'Обзор'}</span>
                </div>
                <div className='addintional-menu'>
                    <img src={reactLogo} alt="" />
                </div>
            </div>
        </header>
    )
} 