import { Link } from 'react-router-dom';
import { useEffect, useState } from 'react';
import reactLogo from '../assets/react.svg'
import {generateKeyFromPassword, generateRandomToken} from '../services/crypto'
import {loginWithNonce} from "../services/auth"
import { AddEvent } from "../services/events";
import { useNavigate } from 'react-router-dom';
import { useLanguage } from '../context/LanguageContext';

export default function Login(){
    const navigate = useNavigate();
    const { t } = useLanguage();

    const Auth = async ()=>{
        const username: string = document.getElementById("login-input").value;
        const password: string  = document.getElementById("password-input").value;

        if(username.length==0||password.length==0)
        {
            AddEvent('info-message', t('messages', 'fillFields'), 'error');
        }

        var key = await generateKeyFromPassword(username, password);
        var token = await generateRandomToken();
        loginWithNonce(username, key).then((state)=>{
            if(!state){
                AddEvent('info-message', t('messages', 'invalidCredentials'), 'error');
            }
            else{
                navigate('/finder');
            }
        })

    }

    return (
        <div className="auth-form">
            <div className="auth-form-head">
               <img src='/logo.png' alt="" />
               <p>{t('login', 'title')}</p>
            </div>
            <div className="auth-form-body">
                    <input
                        type="text"
                        className="auth-form-input"
                        id="login-input"
                        placeholder={t('login', 'usernamePlaceholder')}
                        //DEMO!
                        // defaultValue="guest"
                    />
                    <input
                        type="password"
                        className="auth-form-input"
                        id="password-input"
                        placeholder={t('login', 'passwordPlaceholder')}
                        //DEMO!
                        // placeholder="Пароль: 123"
                    />
                    <div>
                        <text>{t('login', 'noAccount')} </text>
                        <Link to="signup">{t('login', 'signup')}</Link>
                    </div>
            </div>
            <div className="auth-form-confirm">
                <button onClick={Auth}>{t('login', 'submitButton')}</button>
            </div>

        </div>
    );
}