import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import Home from './pages/Home';
import Finder from './pages/Finder';
import Header from './modules/Header';
import MessageEvent from './modules/MessageEvent';
import Login from './modules/Login';
import Signup from "./modules/Signup";
import "./pages/Auth.scss";

function App() {
  return (
    <>
      {/* Контент */}
        <Routes>
          <Route path='/' element={<Home/>}/>
          <Route path='/finder' element={<Finder/>}/>
          <Route path='/auth' element={<Login />}/>
          <Route path='/auth/signup' element={<Signup/>}/> 
          <Route path='*' element={<h2>Стравница не найдена (404)</h2>}/>
        </Routes>
        <MessageEvent/>
    </>
  )
}

export default App;

