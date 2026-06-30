/*
 * AuthView.js
 * Перенос логики AuthViewModel — переключение дочерних экранов авторизации.
 * AuthViewModel стартует с LoginViewModel (CurrentViewModel = LoginViewModel).
 *
 * Карта экранов процесса авторизации (на будущее, при добавлении экранов):
 *   "login"    → LoginView.html
 *   "register" → RegisterView.html
 */
(function () {
  "use strict";

  var viewMap = {
    "login": "LoginView.html"
    // "register": "RegisterView.html"  // пока отсутствует в оригинале
  };

  var authContent = document.getElementById("authContent");

  // Переключение CurrentViewModel (точка расширения).
  window.showAuthView = function (viewName) {
    var file = viewMap[viewName];
    if (file) { authContent.src = file; }
  };

  // Начальное состояние: CurrentViewModel = LoginViewModel.
  authContent.src = viewMap["login"];
})();
