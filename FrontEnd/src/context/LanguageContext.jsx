import { createContext, useContext, useState, useEffect } from 'react';

const LanguageContext = createContext();

const translations = {
    ru: {
        signup: {
            title: 'Регистрация',
            usernamePlaceholder: 'Имя пользователя',
            passwordPlaceholder: 'Пароль',
            haveAccount: 'Уже есть аккаунт?',
            login: 'Войти',
            submitButton: 'Регистрация',
        },
        login: {
            title: 'Вход',
            usernamePlaceholder: 'Имя пользователя',
            passwordPlaceholder: 'Пароль',
            noAccount: 'Нет аккаунта?',
            signup: 'Зарегистрироваться',
            submitButton: 'Войти',
        },
        finder: {
            newFile: 'Новый файл',
            newFolder: 'Новая папка',
            uploadFile: 'Загрузить файл',
        },
        languageSelect: {
            title: 'Выберите язык',
            subtitle: 'Выберите язык / Select language',
        },
        messages: {
            fillFields: 'Заполните все поля!',
            invalidCredentials: 'Неправильный логин или пароль!',
            userExists: 'Пользователь с таким именем уже существует',
            authRequired: 'Для доступа необходимо аутентифицироваться!',
        },
    },
    en: {
        signup: {
            title: 'Sign Up',
            usernamePlaceholder: 'Username',
            passwordPlaceholder: 'Password',
            haveAccount: 'Already have an account?',
            login: 'Log In',
            submitButton: 'Sign Up',
        },
        login: {
            title: 'Log In',
            usernamePlaceholder: 'Username',
            passwordPlaceholder: 'Password',
            noAccount: "Don't have an account?",
            signup: 'Sign Up',
            submitButton: 'Log In',
        },
        finder: {
            newFile: 'New File',
            newFolder: 'New Folder',
            uploadFile: 'Upload File',
        },
        languageSelect: {
            title: 'Select Language',
            subtitle: 'Выберите язык / Select language',
        },
        messages: {
            fillFields: 'Please fill in all fields!',
            invalidCredentials: 'Invalid username or password!',
            userExists: 'User with this name already exists',
            authRequired: 'Authentication required to access',
        },
    },
};

export function LanguageProvider({ children }) {
    const [language, setLanguage] = useState('ru');

    useEffect(() => {
        const savedLanguage = localStorage.getItem('language');
        if (savedLanguage && (savedLanguage === 'ru' || savedLanguage === 'en')) {
            setLanguage(savedLanguage);
        }
    }, []);

    const changeLanguage = (lang) => {
        setLanguage(lang);
        localStorage.setItem('language', lang);
    };

    const t = (section, key) => {
        return translations[language]?.[section]?.[key] || translations['ru'][section]?.[key] || key;
    };

    return (
        <LanguageContext.Provider value={{ language, changeLanguage, t }}>
            {children}
        </LanguageContext.Provider>
    );
}

export function useLanguage() {
    const context = useContext(LanguageContext);
    if (!context) {
        throw new Error('useLanguage must be used within a LanguageProvider');
    }
    return context;
}
