/*
 * MainWindowView.js
 * Логика переключения экранов — аналог MainWindowViewModel + ViewLocator.
 * Карта data-view → View-файл повторяет конвенцию ViewModel→View по имени.
 */
(function () {
  "use strict";

  // Конвенция ViewLocator: имя раздела → файл представления.
  var viewMap = {
    "dashboard": "DashboardView.html",
    "home": "HomeView.html",
    "messenger": "MessengerView.html",
    "groups": "GroupsView.html",
    "external-access": "ExternalAccessView.html",
    "audit": "AuditView.html",
    "settings": "SettingsView.html",
    "account": "AccountView.html"
  };

  // IsSideBarEnabled=true по умолчанию (можно переключить в false для блокировки пунктов).
  var isSideBarEnabled = true;

  var content = document.getElementById("content");
  var navButtons = document.querySelectorAll("[data-view]");

  // Обработчик Show*ViewCommand: меняет CurrentViewModel + подсвечивает активный раздел.
  function showView(viewName) {
    var file = viewMap[viewName];
    if (!file) { return; }

    content.src = file;

    navButtons.forEach(function (btn) {
      btn.classList.toggle("active", btn.getAttribute("data-view") === viewName);
    });
  }

  navButtons.forEach(function (btn) {
    btn.addEventListener("click", function () {
      // IsEnabled={Binding IsSideBarEnabled} для пунктов с data-sidebar-enabled.
      if (btn.dataset.sidebarEnabled === "true" && !isSideBarEnabled) { return; }
      showView(btn.getAttribute("data-view"));
    });
  });

  // Стартовый экран — Dashboard (CurrentViewModel = DashboardViewModel при инициализации).
  showView("dashboard");
})();
