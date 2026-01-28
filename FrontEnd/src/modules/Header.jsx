import React from "react";
import { Link } from "react-router-dom";
import './Header.scss'
import { ReactSVG } from 'react-svg'



export default function Header({path, setPath}){
    let head = path.split('/').at(-2);
    if(head=='')
        head = 'Обзор'
    return (
        <header className="main-header">
            <div className='content-header'>
                <div className={`back-btn ${path=='/'?'op-0':''}`} onClick={()=>setPath(path.split('/').slice(0,-2).join('/')+'/')}>
                    <ReactSVG src="/back-arrow.svg"/>
                </div>
                <div className='content-path'>
                    <span>{path.split('/').slice(1).join('/') != '' ? path.split('/').slice(1,-1).join('/') : 'Обзор'}</span>
                </div>
                <div className='addintional-menu op-0'>
                    <ReactSVG src="/menu-dots.svg"/>
                </div>
            </div>
        </header>
    )
} 