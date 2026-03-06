import { useNavigate } from 'react-router-dom';
import { useEffect } from 'react';
import './LanguageSelect.scss';

export default function LanguageSelect() {
    const navigate = useNavigate();

    const selectLanguage = (lang) => {
        localStorage.setItem('language', lang);
        navigate('/signup');
    };

    return (
        <div className="language-select-container">
            <div className="language-select-card">
                <div className="language-select-head">
                    <img src='/logo.png' alt="DataGuard Logo" />
                    <h1>DataGuard</h1>
                </div>
                <p className="language-select-subtitle">Выберите язык / Select language</p>
                <div className="language-buttons">
                    <button onClick={() => selectLanguage('ru')} className="language-btn">
                        <span className="flag">🇷🇺</span>
                        <span>Русский</span>
                    </button>
                    <button onClick={() => selectLanguage('en')} className="language-btn">
                        <span className="flag">🇬🇧</span>
                        <span>English</span>
                    </button>
                </div>
            </div>
        </div>
    );
}
