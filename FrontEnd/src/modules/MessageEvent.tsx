import React, { useState, useEffect, useCallback, useRef } from 'react';
import { CSSTransition, TransitionGroup } from 'react-transition-group';
import './MessageEvent.scss';

// Интерфейс для сообщения
interface Message {
    id: number;
    message: string;
    type: 'success' | 'info' | 'error';
}

// ------------------------------------------------
// ГЛАВНЫЙ КОМПОНЕНТ: MessageEvent
// ------------------------------------------------
export default function MessageEvent() {
    const [messages, setMessages] = useState<Message[]>([]);
    // Карта для хранения индивидуальных таймеров для каждого сообщения
    const timerMap = useRef<Map<number, NodeJS.Timeout>>(new Map());
    
    // Длительность анимации и авто-скрытия
    const ANIMATION_TIMEOUT = 500;
    const AUTOHIDE_DURATION = 5000;

    // 1. Функция удаления сообщения (с очисткой таймера)
    const removeMessage = useCallback((id: number) => {
        // Очистка таймера (если сообщение удалено вручную)
        const timer = timerMap.current.get(id);
        if (timer) {
            clearTimeout(timer);
            timerMap.current.delete(id);
        }
        
        // Удаление из стейта
        setMessages(prevMessages => prevMessages.filter(msg => msg.id !== id));
    }, []);

    // 2. Обработчик глобального события (создание сообщения и таймера)
    const handleMessageEvent = useCallback((event: CustomEvent) => {
        const newMessage: Message = {
            id: Date.now() + Math.random(),
            ...event.detail
        };
        
        // Добавление в стейт
        setMessages(prevMessages => [...prevMessages, newMessage]);
        
        // Создание индивидуального таймера
        const timerId = setTimeout(() => {
            removeMessage(newMessage.id);
        }, AUTOHIDE_DURATION); 
        
        // Сохранение ссылки на таймер
        timerMap.current.set(newMessage.id, timerId);

    }, [removeMessage]);

    // 3. Эффект для подписки/отписки
    useEffect(() => {
        window.addEventListener('info-message', handleMessageEvent as EventListener);

        return () => {
            window.removeEventListener('info-message', handleMessageEvent as EventListener);
            // Очистка всех таймеров при демонтировании компонента
            timerMap.current.forEach(clearTimeout);
            timerMap.current.clear();
        };
    }, [handleMessageEvent]);
    
    return (
        <div>
            {/* TransitionGroup должен быть элементом DOM, чтобы управлять CSSTransition'ами */}
            <TransitionGroup className="message-container">
                {messages.map(msg => {
                    // Создаем ref для каждого элемента, чтобы избежать findDOMNode ошибки
                    const nodeRef = React.createRef<HTMLDivElement>(); 

                    return (
                        <CSSTransition 
                            key={msg.id} 
                            timeout={ANIMATION_TIMEOUT} 
                            classNames="message"
                            nodeRef={nodeRef} // Передаем ссылку
                        >
                            <div 
                                ref={nodeRef} // DOM-элемент получает ссылку
                                className={`message-item message-item--${msg.type}`}
                            >
                                <div className="message-content">{msg.message}</div>
                                <button 
                                    className="message-close" 
                                    onClick={() => removeMessage(msg.id)}>
                                    &times;
                                </button>
                            </div>
                        </CSSTransition>
                    );
                })}
            </TransitionGroup>
        </div>
    );
}