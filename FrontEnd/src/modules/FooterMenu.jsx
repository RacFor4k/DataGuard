import './FooterMenu.scss'
import React, { useState, useEffect, useRef } from 'react';

const scrollToTop = () => {
  window.scrollTo({
    top: 0,
    behavior: 'smooth' // плавная прокрутка
  });
};

export default function FooterMenu({closed, items, onBlur}){
    const containerRef = useRef(null);  
    const [visible, setVisible] = useState(false);

    useEffect(()=>{
        containerRef.current?.focus();
        requestAnimationFrame(() => setVisible(true));
    },[closed])
    const handleBlur = (e) => {
     setVisible(false); // скрываем анимацией
     setTimeout(()=>onBlur?.(),50);
  };

    return (
        <div className={`no-outline footer-menu-base ${visible ? 'show' : ''} ${closed ? 'd-none' : ''}`}>
            <div className='footer-menu no-select' onBlur={handleBlur} tabIndex={0} ref={containerRef}>
                <div onClick={()=>{ scrollToTop(); handleBlur(); items[0].action();}} className={items[0].className} id={items[0].id}>{items[0].text}</div>
                {items.slice(1).map(item => (
                    <>
                    <div onClick={()=>{ scrollToTop(); handleBlur(); item.action();}} className={`${item.className} b-t-1`} id={item.id}>{item.text}</div>
                    </>
                ))}
            </div>
        </div>
    )
} 