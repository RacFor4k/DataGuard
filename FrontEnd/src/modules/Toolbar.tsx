import { Link } from "react-router-dom";
import './Toolbar.scss'
import reactLogo from '../assets/react.svg'
import React, { useState, useEffect, useRef } from 'react';
import FooterMenu from "./FooterMenu";


export function Toolbar({setItem, setSelectable, selectable, menu}) {
    const [closed, setClosed] = React.useState(true);

    const openMenu = () => {
        setClosed(false);
    };

    const closeMenu = () => {
        setClosed(true);
    };

    const action = selectable ? 'Готово' : 'Выбрать';

    return (
        <header className={`main-toolbar `}>
            <FooterMenu closed={closed} items={menu} onBlur={closeMenu}/>
        <div className='content-toolbar'>
            <div onClick={() => openMenu(menu)} className='new-item-btn'>
            <img className="no-select" src={reactLogo} alt="" />
            </div>
            <div className='select-btn' onClick={()=>setSelectable(!selectable)}>
            <span>{action}</span>
            </div>
            <div className='addintional-menu icon'>
            <img src={reactLogo} alt="" />
            </div>
        </div>
        </header>
    );
}

export function SkeletToolbar() {
    return (
        <header className="main-toolbar">
        <div className='content-toolbar'>
            <div className='new-item-btn'>
            <img className="no-select" src={reactLogo} alt="" />
            </div>
            <div className='select-btn'>
            <span>Выбрать</span>
            </div>
            <div className='addintional-menu icon'>
            <img src={reactLogo} alt="" />
            </div>
        </div>
        </header>
    );
}
