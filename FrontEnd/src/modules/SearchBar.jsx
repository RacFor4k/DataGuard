import searchIcon from '../assets/react.svg'
import './SearchBar.scss'

export default function SearchBar(){
    return(
        <div className="search-wrapper">
            <img src={searchIcon} alt="search" className="search-inside-icon" />
            <input 
                type="text" 
                className="search-input" 
                placeholder="Поиск..." 
            />
        </div>
    );
}
