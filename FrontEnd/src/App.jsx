import { BrowserRouter as Router, Routes, Route, Link, Navigate, useLocation } from 'react-router-dom';
import { useEffect } from 'react';
import Home from './pages/Home';
import Finder from './pages/Finder';
import Header from './modules/Header';
import MessageEvent from './modules/MessageEvent';
import Login from './modules/Login';
import Signup from "./modules/Signup";
import LanguageSelect from './pages/LanguageSelect';
import "./pages/Auth.scss";

function LanguageGuard({ children }) {
    const location = useLocation();
    const language = localStorage.getItem('language');

    // Allow access to language selection page without redirect
    if (location.pathname === '/') {
        return children;
    }

    // If no language selected, redirect to language selection
    if (!language) {
        return <Navigate to="/" replace />;
    }

    return children;
}

function App() {
  return (
    <>
      {/* Content */}
        <Routes>
          <Route path='/' element={<LanguageSelect/>}/>
          <Route path='/signup' element={<LanguageGuard><Signup/></LanguageGuard>}/>
          <Route path='/finder' element={<LanguageGuard><Finder/></LanguageGuard>}/>
          <Route path='/auth' element={<LanguageGuard><Login /></LanguageGuard>}/>
          <Route path='/auth/signup' element={<LanguageGuard><Signup/></LanguageGuard>}/>
          <Route path='*' element={<h2>Page not found (404)</h2>}/>
        </Routes>
        <MessageEvent/>
    </>
  )
}

export default App;

