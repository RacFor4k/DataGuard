/**
 * Блокирует все клики на странице, кроме элемента с указанным id
 * @param {string} elementId - id элемента, на котором разрешены клики
 */
export function blockAllClicksExcept(elementId) {
  const element = document.getElementById(elementId);
  if (!element) {
    console.warn(`Элемент с id="${elementId}" не найден`);
    return;
  }

  // Добавляем класс для блокировки
  document.body.classList.add('clicks-blocked');
  
  // Добавляем специальный класс целевому элементу
  element.classList.add('clicks-allowed');
  
  // Показываем оверлей (опционально)
  const overlay = document.getElementById('click-blocker-overlay');
  if (overlay) {
    overlay.style.display = 'block';
  }
}

/**
 * Снимает блокировку со всей страницы
 */
export function unblockAllClicks() {
  // Убираем класс блокировки
  document.body.classList.remove('clicks-blocked');
  
  // Убираем класс у всех разрешённых элементов
  document.querySelectorAll('.clicks-allowed').forEach(el => {
    el.classList.remove('clicks-allowed');
  });
  
  // Прячем оверлей
  const overlay = document.getElementById('click-blocker-overlay');
  if (overlay) {
    overlay.style.display = 'none';
  }
}