import { AddEvent } from "../services/events"
import { Link } from 'react-router-dom';
import Finder from "./Finder";
import { RandInt } from "../services/crypto";

export default function Home() {
    return (
        <>
        <button onClick={()=>{
            AddEvent('info-message', 'Hello World', ['success','info','error'][RandInt(0,3)])
        }}>Event</button>
        <ul>
            <li><Link to={'/Finder'}>Finder</Link></li>
            <li><Link to={'/auth'}>Login</Link></li>
        </ul>
        </>
    )
}