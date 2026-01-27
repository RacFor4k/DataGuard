import { Link } from 'react-router-dom';
import { useEffect, useState } from 'react';
import reactLogo from '../assets/react.svg'
import {generateKeyFromPassword, generateRandomToken} from '../services/crypto'
import {signup} from "../services/auth"
import { AddEvent } from "../services/events";
import { useNavigate } from 'react-router-dom';

export default function Login(){
    const navigate = useNavigate();

    const Signup = async ()=>{
        const username: string = document.getElementById("login-input").value;
        const password: string  = document.getElementById("password-input").value;
        
        if(username.length==0||password.length==0)
        {
            AddEvent('info-message', 'Заполните все поля!', 'error');
        }

        var key = await generateKeyFromPassword(username, password);
        var token = generateRandomToken();
        signup(username, key, token).then((state)=>{
            if(!state){
                AddEvent('info-message', 'Пользователь с таким именем уже существует', 'error');
            }
            else{
                navigate('/finder');
            }
        })

    }

    return (
        <div className="auth-form">
            <div className="auth-form-head">
               <img src={reactLogo} alt="" />
               <p>Вход в аккаунт</p>
            </div>
            <div className="auth-form-body">
                    <input 
                        type="text" 
                        className="auth-form-input" 
                        id="login-input"
                        placeholder="Имя пользователя" 
                        //DEMO!
                        defaultValue="guest"
                    />
                    <input 
                        type="password" 
                        className="auth-form-input" 
                        id="password-input"
                        placeholder="Пароль" 
                        //DEMO!
                        placeholder="Пароль: 123"
                    />
                    <div>
                        <text>Уже есть аккаунт? </text>
                        <Link to="/auth">Войти</Link>
                    </div>
            </div>
            <div className="auth-form-confirm">
                <button onClick={Signup}>Регистрация</button>
            </div>

        </div>
    );
}