import { AddEvent } from "../services/events"
import { Link } from 'react-router-dom';
import Finder from "./Finder";
import { RandInt } from "../services/crypto";
import { useNavigate } from "react-router-dom";
import { useEffect } from "react";

export default function Home() {
    const navigate = useNavigate();
    useEffect(()=>{
        window.location = window.location+'auth';
    })
    return (
        <>
        {/* <button onClick={()=>{
            AddEvent('info-message', 'Hello World', ['success','info','error'][RandInt(0,3)])
        }}>Event</button> */}
        <ul>
            <li><Link to={'/Finder'}>Finder</Link></li>
            <li><Link to={'/auth'}>Login</Link></li>
        </ul>
        </>
    )
}